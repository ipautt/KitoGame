using KitoGame.Services;

namespace KitoGame.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly AppStateService _appState;

    public SettingsPage(AppStateService appState)
    {
        InitializeComponent();
        _appState = appState;
        LoadCurrentPreferences();
    }

    private void LoadCurrentPreferences()
    {
        SwitchTurnVibration.IsToggled = _appState.VibrationOnTurnEnabled;
        SwitchNotificationSound.IsToggled = _appState.NotificationSoundEnabled;
        UpdateLanguageButtons();
    }

    private void OnTurnVibrationToggled(object? sender, ToggledEventArgs e)
    {
        _appState.VibrationOnTurnEnabled = e.Value;
    }

    private void OnNotificationSoundToggled(object? sender, ToggledEventArgs e)
    {
        _appState.NotificationSoundEnabled = e.Value;
    }

    private void OnTestVibrationClicked(object? sender, EventArgs e)
    {
        _appState.TriggerTurnVibration();
    }

    private void OnTestSoundClicked(object? sender, EventArgs e)
    {
        _appState.PlayNotificationSoundAndVibrate();
    }

    private void OnLangEsClicked(object? sender, EventArgs e)
    {
        _appState.ChangeLanguage("es");
        UpdateLanguageButtons();
    }

    private void OnLangEnClicked(object? sender, EventArgs e)
    {
        _appState.ChangeLanguage("en");
        UpdateLanguageButtons();
    }

    private void OnLangPtClicked(object? sender, EventArgs e)
    {
        _appState.ChangeLanguage("pt");
        UpdateLanguageButtons();
    }

    private void UpdateLanguageButtons()
    {
        string cur = _appState.Localization.GetLanguageCode();
        BtnLangEs.BackgroundColor = cur == "es" ? Color.FromArgb("#F59E0B") : Color.FromArgb("#FFFFFF");
        BtnLangEs.TextColor = cur == "es" ? Color.FromArgb("#1E293B") : Color.FromArgb("#475569");

        BtnLangEn.BackgroundColor = cur == "en" ? Color.FromArgb("#F59E0B") : Color.FromArgb("#FFFFFF");
        BtnLangEn.TextColor = cur == "en" ? Color.FromArgb("#1E293B") : Color.FromArgb("#475569");

        BtnLangPt.BackgroundColor = cur == "pt" ? Color.FromArgb("#F59E0B") : Color.FromArgb("#FFFFFF");
        BtnLangPt.TextColor = cur == "pt" ? Color.FromArgb("#1E293B") : Color.FromArgb("#475569");
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
