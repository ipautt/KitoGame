using KitoGame.Models;
using KitoGame.Services;

namespace KitoGame.Pages;

public partial class WalletPage : ContentPage
{
    private readonly AppStateService _appState;

    public WalletPage(AppStateService appState)
    {
        InitializeComponent();
        _appState = appState;
        PickerPaymentMethod.SelectedIndex = 0;
        PickerPayoutBank.SelectedIndex = 0;
        PickerPayoutType.SelectedIndex = 0;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshWalletUI();
    }

    private void OnToggleW1Tapped(object? sender, EventArgs e)
    {
        W1Content.IsVisible = !W1Content.IsVisible;
        BtnToggleW1.Text = W1Content.IsVisible ? "▲ Recoger" : "▼ Abrir";
    }

    private void OnToggleW2Tapped(object? sender, EventArgs e)
    {
        W2Content.IsVisible = !W2Content.IsVisible;
        BtnToggleW2.Text = W2Content.IsVisible ? "▲ Recoger" : "▼ Abrir";
    }

    private void OnToggleW3Tapped(object? sender, EventArgs e)
    {
        W3Content.IsVisible = !W3Content.IsVisible;
        BtnToggleW3.Text = W3Content.IsVisible ? "▲ Recoger" : "▼ Abrir";
    }

    private void OnToggleW4Tapped(object? sender, EventArgs e)
    {
        W4Content.IsVisible = !W4Content.IsVisible;
        BtnToggleW4.Text = W4Content.IsVisible ? "▲ Recoger" : "▼ Abrir";
    }

    private void OnToggleW5Tapped(object? sender, EventArgs e)
    {
        W5Content.IsVisible = !W5Content.IsVisible;
        BtnToggleW5.Text = W5Content.IsVisible ? "▲ Recoger" : "▼ Abrir";
    }

    private void RefreshWalletUI()
    {
        var user = _appState.CurrentUser;
        if (user == null) return;

        LblUserHeader.Text = $"{user.DisplayName} ({user.Email})";
        LblAvailableBalance.Text = user.FormattedBalance;
        LblLockedBalance.Text = user.FormattedLockedBalance;

        LblCurrentPayoutAccount.Text = user.HasRegisteredPayoutAccount
            ? $"✅ Cuenta Inscrita: {user.PayoutAccountSummary}"
            : "⚠️ Aún no has inscrito una cuenta para recibir tus retiros.";
        LblCurrentPayoutAccount.TextColor = user.HasRegisteredPayoutAccount
            ? Color.FromArgb("#047857")
            : Color.FromArgb("#B45309");

        LblWithdrawDestinationHint.Text = user.HasRegisteredPayoutAccount
            ? $"Destino del retiro: {user.PayoutAccountSummary} (Titular: {user.DisplayName})"
            : "⚠️ Inscribe primero tu cuenta en el recuadro 3 para poder retirar saldo.";

        if (!string.IsNullOrWhiteSpace(user.PayoutAccountNumber))
        {
            EntryPayoutAccountNumber.Text = user.PayoutAccountNumber;
        }
        if (!string.IsNullOrWhiteSpace(user.PayoutDocumentId))
        {
            EntryPayoutDocumentId.Text = user.PayoutDocumentId;
        }

        BuildPaymentRequestsList();
        BuildTransactionsList();
    }

    private void BuildPaymentRequestsList()
    {
        PaymentRequestsContainer.Children.Clear();
        var requests = _appState.GetCurrentUserPaymentRequests();
        int pendingCount = requests.Count(r => r.Status == PaymentStatus.Pending);
        LblW2Title.Text = pendingCount > 0
            ? $"⏳ 2. CONFIRMACIÓN DE PAGOS ({pendingCount} pendiente)"
            : "⏳ 2. CONFIRMACIÓN DE PAGOS";

        if (requests.Count == 0)
        {
            PaymentRequestsContainer.Children.Add(new Label
            {
                Text = "No tienes órdenes de pago generadas.",
                FontSize = 12,
                TextColor = Color.FromArgb("#64748B")
            });
            return;
        }

        foreach (var req in requests.Take(8))
        {
            var card = new Border
            {
                BackgroundColor = req.Status == PaymentStatus.Pending ? Color.FromArgb("#FFFBEB") : Color.FromArgb("#FFFDF7"),
                Stroke = req.Status == PaymentStatus.Pending
                    ? Color.FromArgb("#F59E0B")
                    : (req.Status == PaymentStatus.Confirmed ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444")),
                StrokeThickness = 1.5,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = 12
            };

            var stack = new VerticalStackLayout { Spacing = 6 };
            stack.Children.Add(new Label
            {
                Text = $"{req.ReferenceCode} • {req.FormattedAmount} ({req.PaymentMethod})",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1E293B")
            });

            stack.Children.Add(new Label
            {
                Text = $"{req.StatusText} • {req.FormattedDate}",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = req.Status == PaymentStatus.Pending ? Color.FromArgb("#B45309") : Color.FromArgb("#475569")
            });

            if (req.Status == PaymentStatus.Pending)
            {
                var btnRow = new HorizontalStackLayout { Spacing = 8 };

                var confirmBtn = new Button
                {
                    Text = "✅ Confirmar Pago y Abonar Saldo",
                    BackgroundColor = Color.FromArgb("#10B981"),
                    TextColor = Colors.White,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 12,
                    HeightRequest = 38,
                    CornerRadius = 8
                };
                confirmBtn.Clicked += async (_, _) =>
                {
                    var res = _appState.ConfirmPaymentRequest(req.Id);
                    RefreshWalletUI();
                    await DisplayAlert("Pago Confirmado", res.Message, "OK");
                };

                var rejectBtn = new Button
                {
                    Text = "Cancelar",
                    BackgroundColor = Color.FromArgb("#FEE2E2"),
                    TextColor = Color.FromArgb("#B91C1C"),
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 12,
                    HeightRequest = 38,
                    CornerRadius = 8
                };
                rejectBtn.Clicked += (_, _) =>
                {
                    _appState.RejectPaymentRequest(req.Id);
                    RefreshWalletUI();
                };

                btnRow.Children.Add(confirmBtn);
                btnRow.Children.Add(rejectBtn);
                stack.Children.Add(btnRow);
            }

            card.Content = stack;
            PaymentRequestsContainer.Children.Add(card);
        }
    }

    private void BuildTransactionsList()
    {
        TransactionsContainer.Children.Clear();
        var txs = _appState.GetCurrentUserTransactions();

        if (txs.Count == 0)
        {
            TransactionsContainer.Children.Add(new Label
            {
                Text = "No hay movimientos confirmados.",
                FontSize = 13,
                TextColor = Color.FromArgb("#64748B")
            });
            return;
        }

        foreach (var tx in txs)
        {
            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#FFFDF7"),
                Stroke = Color.FromArgb("#E2E8F0"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = 12
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 10
            };

            var left = new VerticalStackLayout { Spacing = 2 };
            left.Children.Add(new Label
            {
                Text = tx.Description,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1E293B")
            });
            left.Children.Add(new Label
            {
                Text = tx.FormattedDate,
                FontSize = 11,
                TextColor = Color.FromArgb("#64748B")
            });

            var right = new Label
            {
                Text = tx.FormattedAmount,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = tx.AmountColor,
                VerticalOptions = LayoutOptions.Center
            };

            grid.Add(left, 0, 0);
            grid.Add(right, 1, 0);
            card.Content = grid;
            TransactionsContainer.Children.Add(card);
        }
    }

    private void OnPreset2000Clicked(object? sender, EventArgs e) => EntryDepositAmount.Text = "2000";
    private void OnPreset5000Clicked(object? sender, EventArgs e) => EntryDepositAmount.Text = "5000";
    private void OnPreset20000Clicked(object? sender, EventArgs e) => EntryDepositAmount.Text = "20000";

    private async void OnCreatePaymentOrderClicked(object? sender, EventArgs e)
    {
        if (!decimal.TryParse(EntryDepositAmount.Text, out decimal amount) || amount < 1000m)
        {
            await DisplayAlert("Monto inválido", "Ingresa un monto mínimo de $1.000 COP.", "OK");
            return;
        }

        string method = PickerPaymentMethod.SelectedItem?.ToString() ?? "Nequi";
        var res = _appState.CreatePaymentRequest(amount, method);
        if (!res.Success)
        {
            await DisplayAlert("Aviso", res.Message, "OK");
            return;
        }

        // Abrir automáticamente el recuadro 2 para que el usuario vea su orden pendiente lista para confirmar
        W2Content.IsVisible = true;
        BtnToggleW2.Text = "▲ Recoger";
        RefreshWalletUI();
        await DisplayAlert("Orden Generada", res.Message, "Ver Orden");
    }

    private async void OnSavePayoutAccountClicked(object? sender, EventArgs e)
    {
        string bank = PickerPayoutBank.SelectedItem?.ToString() ?? "Nequi";
        string accType = PickerPayoutType.SelectedItem?.ToString() ?? "Billetera Móvil (Celular)";
        string accNumber = EntryPayoutAccountNumber.Text ?? string.Empty;
        string docId = EntryPayoutDocumentId.Text ?? string.Empty;

        var res = _appState.SavePayoutAccount(bank, accType, accNumber, docId);
        if (!res.Success)
        {
            await DisplayAlert("Datos incompletos", res.Message, "OK");
            return;
        }

        RefreshWalletUI();
        await DisplayAlert("Cuenta Inscrita", res.Message, "Perfecto");
    }

    private async void OnWithdrawClicked(object? sender, EventArgs e)
    {
        if (!decimal.TryParse(EntryWithdrawAmount.Text, out decimal amount) || amount <= 0)
        {
            await DisplayAlert("Error", "Ingresa un monto válido para retirar.", "OK");
            return;
        }

        var res = _appState.WithdrawBalance(amount);
        if (!res.Success)
        {
            // Si aún no ha inscrito cuenta, abrir el recuadro 3 automáticamente
            if (!_appState.CurrentUser?.HasRegisteredPayoutAccount ?? false)
            {
                W3Content.IsVisible = true;
                BtnToggleW3.Text = "▲ Recoger";
            }
            await DisplayAlert("Inscribe tu cuenta primero", res.Message, "OK");
            return;
        }

        EntryWithdrawAmount.Text = string.Empty;
        RefreshWalletUI();
        await DisplayAlert("Retiro Exitoso", res.Message, "OK");
    }
}

