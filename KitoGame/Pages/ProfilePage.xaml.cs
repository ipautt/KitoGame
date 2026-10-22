using KitoGame.Services;

namespace KitoGame.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly AppStateService _appState;

    public ProfilePage(AppStateService appState)
    {
        InitializeComponent();
        _appState = appState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadUserData();
        ApplyLocalization();
    }

    private void OnLangEsClicked(object? sender, EventArgs e)
    {
        _appState.ChangeLanguage("es");
        ApplyLocalization();
    }

    private void OnLangEnClicked(object? sender, EventArgs e)
    {
        _appState.ChangeLanguage("en");
        ApplyLocalization();
    }

    private void OnLangPtClicked(object? sender, EventArgs e)
    {
        _appState.ChangeLanguage("pt");
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var loc = _appState.Localization;
        string code = loc.GetLanguageCode();

        HighlightLangButton(BtnLangEs, code == "es");
        HighlightLangButton(BtnLangEn, code == "en");
        HighlightLangButton(BtnLangPt, code == "pt");

        Title = loc.T("ProfileTitle");
        LblSelectLanguage.Text = loc.T("SelectLanguageLabel");
        LblEditHeader.Text = loc.T("EditProfileHeader");
        LblFirstName.Text = loc.T("FirstNameLabel");
        LblLastName.Text = loc.T("LastNameLabel");
        LblEmail.Text = loc.T("EmailLabel");
        LblPhone.Text = loc.T("PhoneLabel");
        LblNewPass.Text = loc.T("NewPasswordLabel");
        BtnSaveProfile.Text = loc.T("BtnSaveProfile");
    }

    private static void HighlightLangButton(Button btn, bool active)
    {
        btn.BackgroundColor = active ? Color.FromArgb("#F59E0B") : Color.FromArgb("#FFFBEB");
        btn.TextColor = active ? Color.FromArgb("#1E293B") : Color.FromArgb("#78350F");
    }

    private void LoadUserData()
    {
        var user = _appState.CurrentUser;
        if (user == null) return;

        string fInit = !string.IsNullOrWhiteSpace(user.FirstName) ? user.FirstName[..1].ToUpperInvariant() : "U";
        string lInit = !string.IsNullOrWhiteSpace(user.LastName) ? user.LastName[..1].ToUpperInvariant() : "";
        LblAvatarInitials.Text = $"{fInit}{lInit}";

        LblFullNameHeader.Text = user.DisplayName;
        LblUsernameBadge.Text = $"@{user.Username} • {user.Email} • W:{user.Wins} | L:{user.Losses}";

        EntryFirstName.Text = user.FirstName;
        EntryLastName.Text = user.LastName;
        EntryEmail.Text = user.Email;
        EntryPhone.Text = user.Phone;
        EntryNewPassword.Text = string.Empty;
    }

    private async void OnSaveProfileClicked(object? sender, EventArgs e)
    {
        var res = _appState.UpdateUserProfile(
            EntryFirstName.Text ?? string.Empty,
            EntryLastName.Text ?? string.Empty,
            EntryEmail.Text ?? string.Empty,
            EntryPhone.Text ?? string.Empty,
            EntryNewPassword.Text);

        if (!res.Success)
        {
            await DisplayAlert("Info", res.Message, "OK");
            return;
        }

        LoadUserData();
        await DisplayAlert("OK", res.Message, "OK");
        await Navigation.PopAsync();
    }
}

