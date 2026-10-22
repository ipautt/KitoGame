using KitoGame.Services;

namespace KitoGame.Pages;

public partial class LoginPage : ContentPage
{
    private readonly AppStateService _appState;
    private bool _isRegisterMode;

    public LoginPage(AppStateService appState)
    {
        InitializeComponent();
        _appState = appState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
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

        LblSelectLanguage.Text = loc.T("SelectLanguageLabel");
        LblAppSubtitle.Text = loc.T("AppSubtitle");
        BtnTabLogin.Text = loc.T("TabLogin");
        BtnTabRegister.Text = loc.T("TabRegister");

        LblFirstName.Text = loc.T("FirstNameLabel");
        LblLastName.Text = loc.T("LastNameLabel");
        LblEmail.Text = loc.T("EmailLabel");
        LblPhone.Text = loc.T("PhoneLabel");
        LblPassword.Text = loc.T("PasswordLabel");

        BtnGoogleSignIn.Text = code switch
        {
            "en" => "🌐 Continue with Google Account",
            "pt" => "🌐 Continuar com Conta do Google",
            _ => "🌐 Continuar con Cuenta de Google"
        };

        if (_isRegisterMode)
        {
            LblFormTitle.Text = loc.T("FormRegisterTitle");
            LblUsernameField.Text = loc.T("UsernameAliasLabel");
            BtnSubmit.Text = loc.T("BtnRegisterSubmit");
        }
        else
        {
            LblFormTitle.Text = loc.T("FormLoginTitle");
            LblUsernameField.Text = loc.T("UsernameOrEmailLabel");
            BtnSubmit.Text = loc.T("BtnLoginSubmit");
        }
    }

    private static void HighlightLangButton(Button btn, bool active)
    {
        btn.BackgroundColor = active ? Color.FromArgb("#F59E0B") : Color.FromArgb("#FFFFFF");
        btn.TextColor = active ? Color.FromArgb("#1E293B") : Color.FromArgb("#475569");
        btn.BorderColor = active ? Color.FromArgb("#D97706") : Color.FromArgb("#CBD5E1");
        btn.BorderWidth = active ? 2 : 1.5;
    }

    private void OnShowLoginClicked(object? sender, EventArgs e)
    {
        _isRegisterMode = false;
        RegisterFieldsContainer.IsVisible = false;
        BtnTabLogin.BackgroundColor = Color.FromArgb("#F59E0B");
        BtnTabLogin.TextColor = Color.FromArgb("#1E293B");
        BtnTabRegister.BackgroundColor = Color.FromArgb("#FFFFFF");
        BtnTabRegister.TextColor = Color.FromArgb("#475569");
        LblFeedback.IsVisible = false;
        ApplyLocalization();
    }

    private void OnShowRegisterClicked(object? sender, EventArgs e)
    {
        _isRegisterMode = true;
        RegisterFieldsContainer.IsVisible = true;
        EntryUsername.Text = string.Empty;
        EntryPassword.Text = string.Empty;
        BtnTabRegister.BackgroundColor = Color.FromArgb("#F59E0B");
        BtnTabRegister.TextColor = Color.FromArgb("#1E293B");
        BtnTabLogin.BackgroundColor = Color.FromArgb("#FFFFFF");
        BtnTabLogin.TextColor = Color.FromArgb("#475569");
        LblFeedback.IsVisible = false;
        ApplyLocalization();
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        string username = EntryUsername.Text ?? string.Empty;
        string password = EntryPassword.Text ?? string.Empty;

        BtnSubmit.IsEnabled = false;
        await _appState.SyncWithMongoAtlasAsync();
        BtnSubmit.IsEnabled = true;

        if (_isRegisterMode)
        {
            var result = _appState.RegisterUser(
                EntryFirstName.Text ?? string.Empty,
                EntryLastName.Text ?? string.Empty,
                EntryEmail.Text ?? string.Empty,
                EntryPhone.Text ?? string.Empty,
                username,
                password);

            if (!result.Success)
            {
                ShowError(result.Message);
                return;
            }
            await _appState.SyncWithMongoAtlasAsync();
            await DisplayAlert("¡Bienvenido a KitoGame!", result.Message, "Entrar al Lobby");
            await Shell.Current.GoToAsync("//LobbyPage");
        }
        else
        {
            var result = _appState.Login(username, password);
            if (!result.Success)
            {
                ShowError(result.Message);
                return;
            }
            await Shell.Current.GoToAsync("//LobbyPage");
        }
    }

    private async void OnGoogleSignInClicked(object? sender, EventArgs e)
    {
        string? email = await DisplayPromptAsync(
            "Iniciar sesión con Google",
            "Ingresa o confirma tu correo de Google (@gmail.com):",
            accept: "Continuar",
            cancel: "Cancelar",
            placeholder: "ejemplo@gmail.com",
            initialValue: "ivan.pautt@gmail.com",
            keyboard: Keyboard.Email);

        if (string.IsNullOrWhiteSpace(email))
            return;

        string? fullName = await DisplayPromptAsync(
            "Perfil de Google",
            "Confirma tu nombre completo de tu cuenta Google:",
            accept: "Entrar a KitoGame",
            cancel: "Cancelar",
            placeholder: "Ivan Pautt",
            initialValue: "Ivan Pautt");

        if (fullName == null)
            return;

        var res = _appState.LoginWithGoogle(email, fullName);
        if (!res.Success)
        {
            ShowError(res.Message);
            return;
        }

        await DisplayAlert("Google Sign-In", res.Message, "Continuar");
        await Shell.Current.GoToAsync("//LobbyPage");
    }

    private void ShowError(string message)
    {
        LblFeedback.Text = message;
        LblFeedback.IsVisible = true;
    }
}

