#pragma warning disable CA1416
using KitoGame.Models;

namespace KitoGame.Services;

/// <summary>
/// Servicio nativo de notificaciones del sistema Android (barra de estado superior del celular).
/// </summary>
public static class NativeNotificationService
{
    private const string ChannelId = "kitogame_alerts_high_v1";
    private const string ChannelName = "Retos y Alertas de KitoGame";
    private static int _notificationIdCounter = 1000;

    public static void EnsurePermissionsAndChannel()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                var manager = context.GetSystemService(Android.Content.Context.NotificationService) as Android.App.NotificationManager;
                if (manager != null)
                {
                    var channel = new Android.App.NotificationChannel(
                        ChannelId,
                        ChannelName,
                        Android.App.NotificationImportance.High)
                    {
                        Description = "Notificaciones de retos recibidos y jugadores buscando rival"
                    };
                    channel.EnableVibration(true);
                    manager.CreateNotificationChannel(channel);
                }
            }

            // En Android 13+ (API 33+) solicitar permiso POST_NOTIFICATIONS al usuario si aún no está concedido
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
            {
                var activity = Platform.CurrentActivity;
                if (activity != null)
                {
                    if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(
                            activity,
                            Android.Manifest.Permission.PostNotifications) != Android.Content.PM.Permission.Granted)
                    {
                        AndroidX.Core.App.ActivityCompat.RequestPermissions(
                            activity,
                            new[] { Android.Manifest.Permission.PostNotifications },
                            5001);
                    }
                }
            }
        }
        catch
        {
            // Ignorar si la actividad aún no está adjunta
        }
#endif
    }

    public static void ShowSystemNotification(AppNotification notification)
    {
#if ANDROID
        try
        {
            EnsurePermissionsAndChannel();

            var context = Android.App.Application.Context;
            var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "com.companyname.kitogame");
            if (intent != null)
            {
                intent.AddFlags(Android.Content.ActivityFlags.ClearTop | Android.Content.ActivityFlags.SingleTop);
            }

            var pendingFlags = Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M
                ? Android.App.PendingIntentFlags.UpdateCurrent | Android.App.PendingIntentFlags.Immutable
                : Android.App.PendingIntentFlags.UpdateCurrent;

            var pendingIntent = intent != null
                ? Android.App.PendingIntent.GetActivity(context, 0, intent, pendingFlags)
                : null;

            int smallIconResId = context.Resources?.GetIdentifier("appicon", "mipmap", context.PackageName) ?? 0;
            if (smallIconResId == 0)
            {
                smallIconResId = Android.Resource.Drawable.IcDialogInfo;
            }

            var builder = new AndroidX.Core.App.NotificationCompat.Builder(context, ChannelId)
                .SetSmallIcon(smallIconResId)
                .SetContentTitle(notification.Title)
                .SetContentText(notification.Message)
                .SetStyle(new AndroidX.Core.App.NotificationCompat.BigTextStyle().BigText(notification.Message))
                .SetPriority(AndroidX.Core.App.NotificationCompat.PriorityHigh)
                .SetDefaults(AndroidX.Core.App.NotificationCompat.DefaultAll)
                .SetAutoCancel(true);

            if (pendingIntent != null)
            {
                builder.SetContentIntent(pendingIntent);
            }

            var manager = AndroidX.Core.App.NotificationManagerCompat.From(context);
            manager.Notify( Interlocked.Increment(ref _notificationIdCounter), builder.Build());
        }
        catch
        {
            // Ignorar si falta permiso del sistema
        }
#endif
    }
}

