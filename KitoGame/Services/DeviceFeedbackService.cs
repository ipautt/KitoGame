#pragma warning disable CA1416
namespace KitoGame.Services;

/// <summary>
/// Servicio multiplataforma y nativo para feedback háptico (vibración) y sonoro.
/// Utiliza APIs nativas de Android con fallback automático a .NET MAUI Essentials.
/// </summary>
public static class DeviceFeedbackService
{
    /// <summary>
    /// Hace vibrar el dispositivo cuando es el turno del jugador.
    /// </summary>
    public static void TriggerTurnVibration() => Vibrate(400);

    /// <summary>
    /// Hace vibrar el dispositivo durante la duración especificada (en milisegundos).
    /// </summary>
    public static void Vibrate(int milliseconds = 400)
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            Android.OS.Vibrator? vibrator = null;

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S) // Android 12+ (API 31)
            {
                var vm = context.GetSystemService(Android.Content.Context.VibratorManagerService) as Android.OS.VibratorManager;
                vibrator = vm?.DefaultVibrator;
            }
            else
            {
                vibrator = context.GetSystemService(Android.Content.Context.VibratorService) as Android.OS.Vibrator;
            }

            if (vibrator != null && vibrator.HasVibrator)
            {
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O) // Android 8+
                {
                    vibrator.Vibrate(Android.OS.VibrationEffect.CreateOneShot(milliseconds, Android.OS.VibrationEffect.DefaultAmplitude));
                }
                else
                {
                    vibrator.Vibrate(milliseconds);
                }
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceFeedbackService.Vibrate] Native Android error: {ex.Message}");
        }
#endif

        // Fallback estándar con .NET MAUI
        try
        {
            if (Microsoft.Maui.Devices.Vibration.Default.IsSupported)
            {
                Microsoft.Maui.Devices.Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(milliseconds));
            }
            else
            {
                Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceFeedbackService.Vibrate] MAUI Fallback error: {ex.Message}");
        }
    }

    /// <summary>
    /// Reproduce el tono sonoro de notificación del sistema y hace un beep de refuerzo.
    /// </summary>
    public static void PlayNotificationSound()
    {
#if ANDROID
        bool played = false;
        try
        {
            var context = Android.App.Application.Context;
            var uri = Android.Media.RingtoneManager.GetDefaultUri(Android.Media.RingtoneType.Notification);
            if (uri != null)
            {
                var ringtone = Android.Media.RingtoneManager.GetRingtone(context, uri);
                if (ringtone != null)
                {
                    if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop)
                    {
                        ringtone.AudioAttributes = new Android.Media.AudioAttributes.Builder()
                            .SetUsage(Android.Media.AudioUsageKind.Notification)
                            .SetContentType(Android.Media.AudioContentType.Sonification)
                            .Build();
                    }
                    ringtone.Play();
                    played = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceFeedbackService.PlayNotificationSound] Ringtone error: {ex.Message}");
        }

        // Si el tono del sistema no se pudo reproducir (ej. dispositivo en modo no molestar parcial o sin tono por defecto),
        // utilizamos ToneGenerator para garantizar un sonido audible.
        if (!played)
        {
            try
            {
                var toneGen = new Android.Media.ToneGenerator(Android.Media.Stream.Notification, 100);
                toneGen.StartTone(Android.Media.Tone.PropBeep, 250);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceFeedbackService.PlayNotificationSound] ToneGenerator error: {ex.Message}");
            }
        }
#endif
    }
}

