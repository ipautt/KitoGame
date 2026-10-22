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
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#CBD5E1"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = 12
            };

            card.Content = new Label
            {
                Text = $"{u.DisplayName} (@{u.Username} • {u.Email})\nDisponible: {u.FormattedBalance} | Custodia: {u.FormattedLockedBalance} | V:{u.Wins} / D:{u.Losses}",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1E293B")
            };

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

