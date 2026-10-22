using KitoGame.Services;

namespace KitoGame.Pages;

public partial class AdminPage : ContentPage
{
    private readonly AppStateService _appState;

    public AdminPage(AppStateService appState)
    {
        InitializeComponent();
        _appState = appState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SliderCommission.Value = (double)_appState.CommissionPercentage;
        RefreshAdminUI((decimal)SliderCommission.Value);
    }

    private void RefreshAdminUI(decimal previewPercentage)
    {
        previewPercentage = Math.Round(previewPercentage, 0);
        LblPlatformRevenue.Text = $"${_appState.PlatformTreasuryBalance:N0} COP";
        LblCurrentPercentage.Text = $"Comisión configurada: {previewPercentage:0}%";

        decimal pot = 2000m;
        decimal comm = Math.Round(pot * (previewPercentage / 100m), 0);
        decimal winnerPrize = pot - comm;

        LblSimulationExample.Text =
            $"• Apuesta J1 ($1.000) + J2 ($1.000) = Pozo Total: ${pot:N0} COP\n" +
            $"• Comisión Plataforma ({previewPercentage:0}%): ${comm:N0} COP\n" +
            $"• Premio Neto al Ganador: ${winnerPrize:N0} COP";

        UsersListContainer.Children.Clear();
        foreach (var u in _appState.Users)
        {
            var card = new Border
            {
                BackgroundColor = u.IsBanned ? Color.FromArgb("#FEF2F2") : Colors.White,
                Stroke = u.IsBanned ? Color.FromArgb("#EF4444") : Color.FromArgb("#CBD5E1"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = 12
            };

            var vStack = new VerticalStackLayout { Spacing = 6 };

            string statusTag = u.IsBanned ? " [🚫 SUSPENDIDO]" : (u.IsAdmin ? " [ADMIN]" : "");
            var infoLabel = new Label
            {
                Text = $"{u.DisplayName} (@{u.Username}){statusTag}\n📧 {u.Email}\n💰 Saldo: {u.FormattedBalance} | Custodia: {u.FormattedLockedBalance} | W:{u.Wins} / L:{u.Losses}",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = u.IsBanned ? Color.FromArgb("#B91C1C") : Color.FromArgb("#1E293B")
            };
            vStack.Children.Add(infoLabel);

            var actionsGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 4
            };

            // 1. Botón Contraseña
            var btnPass = new Button
            {
                Text = "🔑 Clave",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(2, 0),
                HeightRequest = 34,
                BackgroundColor = Color.FromArgb("#FFFBEB"),
                TextColor = Color.FromArgb("#92400E"),
                BorderColor = Color.FromArgb("#FCD34D"),
                BorderWidth = 1,
                CornerRadius = 8
            };
            btnPass.Clicked += async (_, _) =>
            {
                string? newPass = await DisplayPromptAsync(
                    "Restablecer Contraseña",
                    $"Ingresa la nueva contraseña para @{u.Username}:",
                    placeholder: "Ej: kito2026",
                    accept: "Guardar",
                    cancel: "Cancelar");

                if (!string.IsNullOrWhiteSpace(newPass))
                {
                    var res = _appState.ResetUserPassword(u.Id, newPass);
                    await DisplayAlert("Resultado", res.Message, "OK");
                    RefreshAdminUI((decimal)SliderCommission.Value);
                }
            };
            Grid.SetColumn(btnPass, 0);
            actionsGrid.Children.Add(btnPass);

            // 2. Botón Saldo
            var btnBalance = new Button
            {
                Text = "💰 Saldo",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(2, 0),
                HeightRequest = 34,
                BackgroundColor = Color.FromArgb("#ECFDF5"),
                TextColor = Color.FromArgb("#065F46"),
                BorderColor = Color.FromArgb("#A7F3D0"),
                BorderWidth = 1,
                CornerRadius = 8
            };
            btnBalance.Clicked += async (_, _) =>
            {
                string? deltaStr = await DisplayPromptAsync(
                    "Ajustar Saldo",
                    $"Monto a sumar o restar a @{u.Username} (Saldo actual: {u.FormattedBalance}):",
                    placeholder: "Ej: 5000 o -2000",
                    keyboard: Keyboard.Numeric,
                    accept: "Ajustar",
                    cancel: "Cancelar");

                if (decimal.TryParse(deltaStr, out decimal delta))
                {
                    var res = _appState.AdjustUserBalance(u.Id, delta, "Ajuste desde el panel móvil");
                    await DisplayAlert("Resultado", res.Message, "OK");
                    RefreshAdminUI((decimal)SliderCommission.Value);
                }
            };
            Grid.SetColumn(btnBalance, 1);
            actionsGrid.Children.Add(btnBalance);

            // 3. Botón Suspender / Reactivar
            var btnBan = new Button
            {
                Text = u.IsBanned ? "✅ Activar" : "🚫 Bloquear",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(2, 0),
                HeightRequest = 34,
                BackgroundColor = u.IsBanned ? Color.FromArgb("#ECFDF5") : Color.FromArgb("#FEF2F2"),
                TextColor = u.IsBanned ? Color.FromArgb("#065F46") : Color.FromArgb("#991B1B"),
                BorderColor = u.IsBanned ? Color.FromArgb("#A7F3D0") : Color.FromArgb("#FCA5A5"),
                BorderWidth = 1,
                CornerRadius = 8
            };
            btnBan.Clicked += async (_, _) =>
            {
                bool confirm = await DisplayAlert(
                    u.IsBanned ? "Reactivar Usuario" : "Suspender Usuario",
                    $"¿Deseas {(u.IsBanned ? "reactivar" : "suspender")} la cuenta de @{u.Username}?",
                    "Sí",
                    "Cancelar");

                if (confirm)
                {
                    var res = _appState.ToggleBanUser(u.Id, "Suspensión administrativa");
                    await DisplayAlert("Resultado", res.Message, "OK");
                    RefreshAdminUI((decimal)SliderCommission.Value);
                }
            };
            Grid.SetColumn(btnBan, 2);
            actionsGrid.Children.Add(btnBan);

            // 4. Botón Eliminar
            if (!u.IsAdmin)
            {
                var btnDelete = new Button
                {
                    Text = "🗑️ Borrar",
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold,
                    Padding = new Thickness(2, 0),
                    HeightRequest = 34,
                    BackgroundColor = Color.FromArgb("#FEF2F2"),
                    TextColor = Color.FromArgb("#DC2626"),
                    BorderColor = Color.FromArgb("#FCA5A5"),
                    BorderWidth = 1,
                    CornerRadius = 8
                };
                btnDelete.Clicked += async (_, _) =>
                {
                    bool confirm = await DisplayAlert(
                        "Eliminar Usuario",
                        $"¿Seguro que deseas eliminar definitivamente a @{u.Username} de la base de datos?",
                        "Sí, Eliminar",
                        "Cancelar");

                    if (confirm)
                    {
                        var res = _appState.DeleteUser(u.Id);
                        await DisplayAlert("Resultado", res.Message, "OK");
                        RefreshAdminUI((decimal)SliderCommission.Value);
                    }
                };
                Grid.SetColumn(btnDelete, 3);
                actionsGrid.Children.Add(btnDelete);
            }

            vStack.Children.Add(actionsGrid);
            card.Content = vStack;
            UsersListContainer.Children.Add(card);
        }
    }

    private void OnSliderCommissionChanged(object? sender, ValueChangedEventArgs e)
    {
        decimal val = Math.Round((decimal)e.NewValue, 0);
        RefreshAdminUI(val);
    }

    private void OnSet0Clicked(object? sender, EventArgs e) => SliderCommission.Value = 0;
    private void OnSet5Clicked(object? sender, EventArgs e) => SliderCommission.Value = 5;
    private void OnSet10Clicked(object? sender, EventArgs e) => SliderCommission.Value = 10;
    private void OnSet15Clicked(object? sender, EventArgs e) => SliderCommission.Value = 15;
    private void OnSet20Clicked(object? sender, EventArgs e) => SliderCommission.Value = 20;

    private async void OnSaveCommissionClicked(object? sender, EventArgs e)
    {
        decimal val = Math.Round((decimal)SliderCommission.Value, 0);
        var res = _appState.UpdateCommissionPercentage(val);
        RefreshAdminUI(val);
        await DisplayAlert("Configuración Guardada", res.Message, "OK");
    }

    private async void OnResetDemoDataClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Reiniciar Base de Datos Temporal",
            "¿Restaurar la base de datos temporal a su estado limpio inicial?",
            "Sí, Reiniciar",
            "Cancelar");

        if (!confirm) return;

        _appState.ResetTemporaryDatabase();
        SliderCommission.Value = (double)_appState.CommissionPercentage;
        RefreshAdminUI(_appState.CommissionPercentage);
        await DisplayAlert("Listo", "Base de datos temporal reiniciada.", "OK");
    }
}

