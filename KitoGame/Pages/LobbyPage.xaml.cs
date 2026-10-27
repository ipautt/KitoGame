using KitoGame.Models;
using KitoGame.Services;

namespace KitoGame.Pages;

public partial class LobbyPage : ContentPage
{
    private readonly AppStateService _appState;
    private string _selectedGameId = "checkers";
    private string? _selectedTargetUserId;
    private bool _isPracticeBotSelected;
    private string _selectedRivalDisplay = "Reto Abierto";
    private List<(string Label, string? UserId, bool IsBot, string DisplayName)> _opponentOptions = new();
    private IDispatcherTimer? _lobbySyncTimer;
    private bool _isSyncing;

    public LobbyPage(AppStateService appState)
    {
        InitializeComponent();
        _appState = appState;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        NativeNotificationService.EnsurePermissionsAndChannel();

        if (_appState.CurrentUser == null)
        {
            var firstUser = _appState.Users.FirstOrDefault();
            if (firstUser != null)
            {
                _appState.Login(firstUser.Username, firstUser.Password);
            }
        }
        RefreshAllUI();

        bool ok = await _appState.SyncWithMongoAtlasAsync();
        LblMongoStatus.Text = ok
            ? $"🟢 MongoDB Atlas En Línea ({_appState.Users.Count} usuarios)"
            : "☁️ MongoDB Atlas: Modo Híbrido / Local";
        RefreshAllUI();

        StartLobbyLiveSyncTimer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _lobbySyncTimer?.Stop();
    }

    private void StartLobbyLiveSyncTimer()
    {
        _lobbySyncTimer?.Stop();
        _lobbySyncTimer = Dispatcher.CreateTimer();
        _lobbySyncTimer.Interval = TimeSpan.FromSeconds(2);
        _lobbySyncTimer.Tick += async (_, _) =>
        {
            if (_isSyncing) return;
            _isSyncing = true;
            try
            {
                bool ok = await _appState.SyncWithMongoAtlasAsync();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (ok)
                    {
                        LblMongoStatus.Text = $"🟢 MongoDB Atlas En Línea ({_appState.Users.Count} usuarios)";
                        RefreshAllUI();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LobbyLiveSync] Error: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
        };
        _lobbySyncTimer.Start();
    }

    private async void OnRefreshChallengesClicked(object? sender, EventArgs e)
    {
        BtnRefreshChallenges.IsEnabled = false;
        BtnRefreshChallenges.Text = "⏳...";
        try
        {
            await _appState.SyncWithMongoAtlasAsync();
            RefreshAllUI();
        }
        finally
        {
            BtnRefreshChallenges.Text = "🔄 Actualizar";
            BtnRefreshChallenges.IsEnabled = true;
        }
    }

    private async void OnSyncMongoClicked(object? sender, EventArgs e)
    {
        BtnSyncCloud.Text = "⏳...";
        bool ok = await _appState.SyncWithMongoAtlasAsync();
        BtnSyncCloud.Text = "🔄 Sincronizar";
        LblMongoStatus.Text = ok
            ? $"🟢 MongoDB Atlas Sincronizado ({_appState.Users.Count} usuarios)"
            : "⚠️ Verifica acceso IP 0.0.0.0/0 en Atlas";
        RefreshAllUI();
    }

    private void OnToggleSection1Tapped(object? sender, EventArgs e)
    {
        Section1Content.IsVisible = !Section1Content.IsVisible;
        BtnToggleSec1.Text = Section1Content.IsVisible ? "▲ Recoger" : "▼ Abrir";
    }

    private void OnToggleSection2Tapped(object? sender, EventArgs e)
    {
        Section2Content.IsVisible = !Section2Content.IsVisible;
        BtnToggleSec2.Text = Section2Content.IsVisible ? "▲ Recoger" : "▼ Abrir";
    }

    private void OnToggleSection3Tapped(object? sender, EventArgs e)
    {
        Section3Content.IsVisible = !Section3Content.IsVisible;
        BtnToggleSec3.Text = Section3Content.IsVisible ? "▲ Cerrar" : "▼ Abrir";
    }

    private void OnToggleSection4Tapped(object? sender, EventArgs e)
    {
        Section4Content.IsVisible = !Section4Content.IsVisible;
        BtnToggleSec4.Text = Section4Content.IsVisible ? "▲ Recoger" : "▼ Abrir";
    }

    private void OnToggleSection5Tapped(object? sender, EventArgs e)
    {
        Section5Content.IsVisible = !Section5Content.IsVisible;
        BtnToggleSec5.Text = Section5Content.IsVisible ? "▲ Recoger" : "▼ Abrir";
    }

    private void PopulateRealOpponentsPicker()
    {
        string? prevUserId = _selectedTargetUserId;
        bool prevBot = _isPracticeBotSelected;

        _opponentOptions.Clear();
        _opponentOptions.Add(("🌍 Cualquier Jugador Conectado (Reto Abierto)", null, false, "Reto Abierto"));

        foreach (var u in _appState.GetRealOpponents())
        {
            _opponentOptions.Add(($"👤 {u.DisplayName} (@{u.Username})", u.Id, false, u.DisplayName));
        }

        _opponentOptions.Add(("🤖 Bot Kito (Modo Práctica Solo)", null, true, "Bot Kito"));

        PickerRealOpponent.SelectedIndexChanged -= OnOpponentPickerChanged;
        PickerRealOpponent.ItemsSource = _opponentOptions.Select(o => o.Label).ToList();

        int idxToSelect = 0;
        if (prevBot)
        {
            idxToSelect = _opponentOptions.Count - 1;
        }
        else if (!string.IsNullOrEmpty(prevUserId))
        {
            int found = _opponentOptions.FindIndex(o => o.UserId == prevUserId);
            if (found >= 0) idxToSelect = found;
        }

        PickerRealOpponent.SelectedIndex = idxToSelect;
        ApplyOpponentSelection(idxToSelect);
        PickerRealOpponent.SelectedIndexChanged += OnOpponentPickerChanged;
    }

    private void OnOpponentPickerChanged(object? sender, EventArgs e)
    {
        if (PickerRealOpponent.SelectedIndex >= 0 && PickerRealOpponent.SelectedIndex < _opponentOptions.Count)
        {
            ApplyOpponentSelection(PickerRealOpponent.SelectedIndex);
        }
    }

    private void ApplyOpponentSelection(int index)
    {
        var opt = _opponentOptions[index];
        _selectedTargetUserId = opt.UserId;
        _isPracticeBotSelected = opt.IsBot;
        _selectedRivalDisplay = opt.DisplayName;

        if (opt.IsBot)
        {
            LblDuelRivalAvatar.Text = "🤖";
            LblDuelRivalName.Text = "Bot Kito";
            LblDuelRivalStatus.Text = "⚡ Práctica inmediata";
        }
        else if (!string.IsNullOrEmpty(opt.UserId))
        {
            LblDuelRivalAvatar.Text = "🎯";
            LblDuelRivalName.Text = opt.DisplayName;
            LblDuelRivalStatus.Text = "🟢 Jugador registrado";
        }
        else
        {
            LblDuelRivalAvatar.Text = "🌍";
            LblDuelRivalName.Text = "Reto Abierto";
            LblDuelRivalStatus.Text = "🟢 Cualquier jugador";
        }

        UpdatePotPreview();
    }

    private void OnLangEsClicked(object? sender, EventArgs e)
    {
        _appState.ChangeLanguage("es");
        RefreshAllUI();
    }

    private void OnLangEnClicked(object? sender, EventArgs e)
    {
        _appState.ChangeLanguage("en");
        RefreshAllUI();
    }

    private void OnLangPtClicked(object? sender, EventArgs e)
    {
        _appState.ChangeLanguage("pt");
        RefreshAllUI();
    }

    private static void HighlightLangButton(Button btn, bool active)
    {
        btn.BackgroundColor = active ? Color.FromArgb("#F59E0B") : Color.FromArgb("#FFFFFF");
        btn.TextColor = active ? Color.FromArgb("#1E293B") : Color.FromArgb("#334155");
        btn.BorderColor = active ? Color.FromArgb("#D97706") : Color.FromArgb("#CBD5E1");
    }

    private string GetLocalizedGameName(string gameId)
    {
        if (gameId.Equals("checkers", StringComparison.OrdinalIgnoreCase))
            return _appState.Localization.T("GameCheckersName");
        if (gameId.Equals("parchis", StringComparison.OrdinalIgnoreCase))
            return _appState.Localization.T("GameParchisName");
        return _appState.Localization.T("GameChessName");
    }

    private string GetLocalizedGameDesc(string gameId)
    {
        if (gameId.Equals("checkers", StringComparison.OrdinalIgnoreCase))
            return _appState.Localization.T("GameCheckersDesc");
        if (gameId.Equals("parchis", StringComparison.OrdinalIgnoreCase))
            return _appState.Localization.T("GameParchisDesc");
        return _appState.Localization.T("GameChessDesc");
    }

    private void RefreshAllUI()
    {
        var user = _appState.CurrentUser;
        if (user == null) return;

        var loc = _appState.Localization;
        string code = loc.GetLanguageCode();

        HighlightLangButton(BtnLangEs, code == "es");
        HighlightLangButton(BtnLangEn, code == "en");
        HighlightLangButton(BtnLangPt, code == "pt");

        LblWelcomeUser.Text = user.DisplayName;
        LblDuelMyName.Text = user.DisplayName;
        LblUserEmailAndStats.Text = $"{user.Email} ({user.AuthProvider}) • W:{user.Wins} / L:{user.Losses}";
        LblAvailTitle.Text = loc.T("AvailableBalance");
        LblLockedTitle.Text = loc.T("LockedBalance");
        LblAvailableBalance.Text = user.FormattedBalance;
        LblLockedBalance.Text = user.FormattedLockedBalance;

        BtnProfile.Text = loc.T("BtnProfile");
        BtnWallet.Text = loc.T("BtnWallet");
        BtnAdmin.Text = loc.T("BtnAdmin");
        BtnLogout.Text = loc.T("BtnLogout");

        // Visibilidad exclusiva de herramientas de Administrador (Comisión, MongoDB Atlas, Pruebas)
        bool isAdmin = user.IsAdmin;
        AdminOnlyToolbar.IsVisible = isAdmin;
        AdminSimulateNotifGrid.IsVisible = isAdmin;

        decimal comm = _appState.CommissionPercentage;
        LblCommissionInfo.Text = comm == 0
            ? loc.T("CommissionZeroInfo")
            : loc.Format("CommissionActiveInfo", comm.ToString("0.#"), (100m - comm).ToString("0.#"));

        LblSection1Games.Text = loc.T("Section1Games");
        LblSelectedGameHeader.Text = loc.Format("Section2CreateMatch", GetLocalizedGameName(_selectedGameId).ToUpperInvariant());
        LblBetPrompt.Text = loc.T("BetPrompt");
        BtnStartMatchNow.Text = loc.T("BtnStartMatchNow");

        int activeCount = _appState.Challenges.Count(c => c.Status == MatchStatus.WaitingForOpponent || c.Status == MatchStatus.InProgress);
        LblSection3Active.Text = $"🏆 3. RETOS EN LÍNEA Y PARTIDAS ({activeCount})";

        PopulateRealOpponentsPicker();
        UpdatePotPreview();
        BuildGameCatalog();
        BuildActiveChallengesList();
        BuildNotificationsFeed();
        BuildCompletedChallengesList();
    }

    private void BuildNotificationsFeed()
    {
        NotificationsListContainer.Children.Clear();
        int unread = _appState.GetUnreadNotificationsCount();
        BadgeUnreadCount.IsVisible = unread > 0;
        LblUnreadCount.Text = unread.ToString();

        var notifications = _appState.GetCurrentUserNotifications().Take(5).ToList();
        if (notifications.Count == 0)
        {
            NotificationsListContainer.Children.Add(new Label
            {
                Text = "No tienes notificaciones recientes.",
                FontSize = 12,
                TextColor = Color.FromArgb("#64748B")
            });
            return;
        }

        foreach (var notif in notifications)
        {
            bool isDirectChallenge = notif.Type == NotificationType.DirectChallengeReceived;
            bool isSearching = notif.Type == NotificationType.PlayerSearchingOpponent;

            var card = new Border
            {
                BackgroundColor = notif.IsRead ? Color.FromArgb("#FFFDF7") : Color.FromArgb("#FFFBEB"),
                Stroke = isDirectChallenge
                    ? Color.FromArgb("#F59E0B")
                    : (isSearching ? Color.FromArgb("#3B82F6") : Color.FromArgb("#CBD5E1")),
                StrokeThickness = notif.IsRead ? 1 : 1.5,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = 10
            };

            var stack = new VerticalStackLayout { Spacing = 4 };
            stack.Children.Add(new Label
            {
                Text = $"{notif.Title} • {notif.FormattedTime}",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = isDirectChallenge ? Color.FromArgb("#B45309") : Color.FromArgb("#1D4ED8")
            });

            stack.Children.Add(new Label
            {
                Text = notif.Message,
                FontSize = 12,
                TextColor = Color.FromArgb("#334155")
            });

            if (!string.IsNullOrEmpty(notif.RelatedChallengeId))
            {
                var relatedChallenge = _appState.Challenges.FirstOrDefault(c =>
                    c.Id == notif.RelatedChallengeId && c.Status == MatchStatus.WaitingForOpponent);

                if (relatedChallenge != null && relatedChallenge.CreatorId != _appState.CurrentUser?.Id)
                {
                    var acceptFromNotifBtn = new Button
                    {
                        Text = $"✅ Aceptar Reto de {relatedChallenge.CreatorUsername} ({relatedChallenge.FormattedBet})",
                        BackgroundColor = Color.FromArgb("#10B981"),
                        TextColor = Colors.White,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 12,
                        HeightRequest = 36,
                        CornerRadius = 8,
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    acceptFromNotifBtn.Clicked += async (_, _) => await HandleAcceptChallengeAsync(relatedChallenge.Id);
                    stack.Children.Add(acceptFromNotifBtn);
                }
            }

            card.Content = stack;
            NotificationsListContainer.Children.Add(card);
        }
    }

    private void OnMarkNotificationsReadClicked(object? sender, EventArgs e)
    {
        _appState.MarkAllNotificationsAsRead();
        BuildNotificationsFeed();
    }

    private void OnSimulateDirectChallengeNotificationClicked(object? sender, EventArgs e)
    {
        if (!decimal.TryParse(EntryBetAmount.Text, out decimal betAmount) || betAmount < 1000m)
            betAmount = 1000m;

        _appState.ReceiveNewIncomingChallenge(_selectedGameId, betAmount, isDirectChallengeToMe: true);
        BuildNotificationsFeed();
        BuildActiveChallengesList();

        int activeCount = _appState.Challenges.Count(c => c.Status == MatchStatus.WaitingForOpponent || c.Status == MatchStatus.InProgress);
        LblSection3Active.Text = $"🏆 3. RETOS EN LÍNEA Y PARTIDAS ({activeCount})";
    }

    private void OnSimulateOpenSearchNotificationClicked(object? sender, EventArgs e)
    {
        if (!decimal.TryParse(EntryBetAmount.Text, out decimal betAmount) || betAmount < 1000m)
            betAmount = 2000m;

        _appState.ReceiveNewIncomingChallenge(_selectedGameId, betAmount, isDirectChallengeToMe: false);
        BuildNotificationsFeed();
        BuildActiveChallengesList();

        int activeCount = _appState.Challenges.Count(c => c.Status == MatchStatus.WaitingForOpponent || c.Status == MatchStatus.InProgress);
        LblSection3Active.Text = $"🏆 3. RETOS EN LÍNEA Y PARTIDAS ({activeCount})";
    }

    private void BuildGameCatalog()
    {
        GameCatalogContainer.Children.Clear();
        var games = _appState.GameRegistry.GetAvailableGames();
        var loc = _appState.Localization;

        foreach (var game in games)
        {
            bool isSelected = game.Id.Equals(_selectedGameId, StringComparison.OrdinalIgnoreCase);

            var card = new Border
            {
                BackgroundColor = isSelected ? Color.FromArgb("#FFFBEB") : Color.FromArgb("#FFFDF7"),
                Stroke = isSelected ? Color.FromArgb("#F59E0B") : Color.FromArgb("#E2E8F0"),
                StrokeThickness = isSelected ? 2 : 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = 12
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 12
            };

            var iconLbl = new Label
            {
                Text = game.Icon,
                FontSize = 28,
                VerticalOptions = LayoutOptions.Center
            };

            var infoStack = new VerticalStackLayout { Spacing = 2 };
            infoStack.Children.Add(new Label
            {
                Text = GetLocalizedGameName(game.Id),
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1E293B")
            });
            infoStack.Children.Add(new Label
            {
                Text = GetLocalizedGameDesc(game.Id),
                FontSize = 11,
                TextColor = Color.FromArgb("#64748B")
            });

            var selectBtn = new Button
            {
                Text = isSelected ? loc.T("BtnSelected") : loc.T("BtnChoose"),
                BackgroundColor = isSelected ? Color.FromArgb("#10B981") : Color.FromArgb("#F59E0B"),
                TextColor = isSelected ? Colors.White : Color.FromArgb("#1E293B"),
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                HeightRequest = 36,
                CornerRadius = 8,
                VerticalOptions = LayoutOptions.Center
            };

            selectBtn.Clicked += (_, _) =>
            {
                _selectedGameId = game.Id;
                LblSelectedGameHeader.Text = loc.Format("Section2CreateMatch", GetLocalizedGameName(game.Id).ToUpperInvariant());
                BuildGameCatalog();
            };

            grid.Add(iconLbl, 0, 0);
            grid.Add(infoStack, 1, 0);
            grid.Add(selectBtn, 2, 0);
            card.Content = grid;

            GameCatalogContainer.Children.Add(card);
        }
    }

    private void BuildActiveChallengesList()
    {
        ActiveChallengesContainer.Children.Clear();
        var loc = _appState.Localization;
        string? myUserId = _appState.CurrentUser?.Id;

        var active = _appState.Challenges
            .Where(c => c.Status == MatchStatus.WaitingForOpponent || c.Status == MatchStatus.InProgress)
            .ToList();

        if (active.Count == 0)
        {
            ActiveChallengesContainer.Children.Add(new Label
            {
                Text = "No hay retos activos en este momento. ¡Crea uno en el recuadro 2 para retar a tus amigos!",
                FontSize = 13,
                TextColor = Color.FromArgb("#64748B")
            });
            return;
        }

        foreach (var ch in active)
        {
            decimal commRate = _appState.CommissionPercentage;
            decimal commAmount = Math.Round(ch.TotalPot * (commRate / 100m), 0);
            decimal prize = ch.TotalPot - commAmount;

            bool isMyCreatedChallenge = ch.CreatorId == myUserId;

            var card = new Border
            {
                BackgroundColor = ch.Status == MatchStatus.InProgress ? Color.FromArgb("#EFF6FF") : Color.FromArgb("#FFFBEB"),
                Stroke = ch.Status == MatchStatus.InProgress ? Color.FromArgb("#3B82F6") : Color.FromArgb("#F59E0B"),
                StrokeThickness = 1.5,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                Padding = 12
            };

            var stack = new VerticalStackLayout { Spacing = 8 };

            var topRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            topRow.Add(new Label
            {
                Text = $"{ch.GameIcon} {GetLocalizedGameName(ch.GameId)} • #{ch.Id}",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1E293B")
            }, 0, 0);

            topRow.Add(new Label
            {
                Text = ch.StatusBadge,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = ch.Status == MatchStatus.InProgress ? Color.FromArgb("#1D4ED8") : Color.FromArgb("#B45309")
            }, 1, 0);

            stack.Children.Add(topRow);

            var vsRow = new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#E2E8F0"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(10, 8)
            };

            string vsText;
            if (ch.Status == MatchStatus.InProgress)
            {
                vsText = $"👑 {ch.CreatorUsername}   ⚔️ VS ⚔️   🎯 {ch.OpponentUsername}";
            }
            else if (isMyCreatedChallenge)
            {
                string dest = string.IsNullOrEmpty(ch.TargetOpponentUsername)
                    ? "Cualquier jugador conectado"
                    : ch.TargetOpponentUsername;
                vsText = $"⏳ Tu reto está esperando a: {dest}";
            }
            else
            {
                vsText = string.IsNullOrEmpty(ch.TargetOpponentId)
                    ? $"📢 {ch.CreatorUsername} busca quién lo rete por {ch.FormattedBet}"
                    : $"⚔️ ¡{ch.CreatorUsername} te reta directamente por {ch.FormattedBet}!";
            }

            vsRow.Content = new Label
            {
                Text = vsText,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#334155"),
                HorizontalTextAlignment = TextAlignment.Center
            };
            stack.Children.Add(vsRow);

            stack.Children.Add(new Label
            {
                Text = $"Apuesta 1v1: {ch.FormattedBet} | {loc.T("PotLabel")}: {ch.FormattedPot} → Premio Ganador: ${prize:N0} COP",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#047857")
            });

            if (ch.Status == MatchStatus.InProgress)
            {
                var playBtn = new Button
                {
                    Text = loc.T("BtnContinueMatch"),
                    BackgroundColor = Color.FromArgb("#2563EB"),
                    TextColor = Colors.White,
                    FontAttributes = FontAttributes.Bold,
                    CornerRadius = 10,
                    HeightRequest = 40
                };
                playBtn.Clicked += async (_, _) => await OpenMatchBoardAsync(ch.Id);
                stack.Children.Add(playBtn);
            }
            else if (isMyCreatedChallenge)
            {
                var waitBoardBtn = new Button
                {
                    Text = "⏳ Entrar a la Sala de Partida (Esperando Rival)",
                    BackgroundColor = Color.FromArgb("#F59E0B"),
                    TextColor = Color.FromArgb("#1E293B"),
                    FontAttributes = FontAttributes.Bold,
                    CornerRadius = 10,
                    HeightRequest = 40
                };
                waitBoardBtn.Clicked += async (_, _) => await OpenMatchBoardAsync(ch.Id);
                stack.Children.Add(waitBoardBtn);
            }
            else
            {
                var acceptBtn = new Button
                {
                    Text = $"⚔️ Aceptar Reto de {ch.CreatorUsername} ({ch.FormattedBet})",
                    BackgroundColor = Color.FromArgb("#10B981"),
                    TextColor = Colors.White,
                    FontAttributes = FontAttributes.Bold,
                    CornerRadius = 10,
                    HeightRequest = 40
                };
                acceptBtn.Clicked += async (_, _) => await HandleAcceptChallengeAsync(ch.Id);
                stack.Children.Add(acceptBtn);
            }

            card.Content = stack;
            ActiveChallengesContainer.Children.Add(card);
        }
    }

    private void BuildCompletedChallengesList()
    {
        CompletedChallengesContainer.Children.Clear();
        var myUser = _appState.CurrentUser;
        bool isAdmin = myUser?.IsAdmin == true;
        string? myUserId = myUser?.Id;

        LblSection4History.Text = isAdmin 
            ? "📜 5. HISTORIAL DE PARTIDAS (GLOBAL ADMIN)" 
            : "📜 5. MI HISTORIAL DE PARTIDAS FINALIZADAS";

        // Filtro de privacidad: solo partidas donde el usuario participó (como creador o rival), excepto administradores
        var finished = _appState.Challenges
            .Where(c => c.Status == MatchStatus.Completed || c.Status == MatchStatus.Draw)
            .Where(c => isAdmin || (!string.IsNullOrEmpty(myUserId) && (c.CreatorId == myUserId || c.OpponentId == myUserId)))
            .Take(12)
            .ToList();

        if (finished.Count == 0)
        {
            CompletedChallengesContainer.Children.Add(new Label
            {
                Text = isAdmin 
                    ? "Aún no hay partidas finalizadas en la plataforma." 
                    : "Aún no tienes partidas finalizadas en tu cuenta.",
                FontSize = 12,
                TextColor = Color.FromArgb("#64748B")
            });
            return;
        }

        foreach (var ch in finished)
        {
            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#FFFDF7"),
                Stroke = Color.FromArgb("#E2E8F0"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = 10
            };

            string summary;
            if (ch.Status == MatchStatus.Draw)
            {
                summary = $"{ch.GameIcon} {GetLocalizedGameName(ch.GameId)} (#{ch.Id}): 🤝 Empate / Tablas";
            }
            else
            {
                bool iWon = !string.IsNullOrEmpty(myUserId) && ch.WinnerUserId == myUserId;
                string winTag = iWon ? "🎉 ¡Ganaste!" : $"🏆 Ganador: {ch.WinnerUsername}";
                summary = $"{ch.GameIcon} {GetLocalizedGameName(ch.GameId)} (#{ch.Id}): {winTag} → +${ch.WinnerPrize:N0} COP";
            }

            card.Content = new Label
            {
                Text = summary,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#334155")
            };

            CompletedChallengesContainer.Children.Add(card);
        }
    }

    private void UpdatePotPreview()
    {
        if (!decimal.TryParse(EntryBetAmount.Text, out decimal bet) || bet <= 0)
        {
            LblPotPreview.Text = "$1.000 COP";
            return;
        }

        decimal pot = bet * 2;
        decimal commRate = _appState.CommissionPercentage;
        decimal comm = Math.Round(pot * (commRate / 100m), 0);
        decimal winnerPrize = pot - comm;

        LblPotPreview.Text = $"Tú (${bet:N0}) vs {_selectedRivalDisplay} (${bet:N0}) = Pozo ${pot:N0} → 🏆 Premio: ${winnerPrize:N0} COP";
    }

    private void OnBetAmountTextChanged(object? sender, TextChangedEventArgs e) => UpdatePotPreview();
    private void OnQuickBet1000Clicked(object? sender, EventArgs e) => EntryBetAmount.Text = "1000";
    private void OnQuickBet2000Clicked(object? sender, EventArgs e) => EntryBetAmount.Text = "2000";
    private void OnQuickBet5000Clicked(object? sender, EventArgs e) => EntryBetAmount.Text = "5000";

    private async void OnCreateChallengeClicked(object? sender, EventArgs e)
    {
        if (!decimal.TryParse(EntryBetAmount.Text, out decimal betAmount))
        {
            await DisplayAlert("Error", "Ingresa un valor numérico válido (ej: 1000).", "OK");
            return;
        }

        var result = await _appState.CreateChallengeAsync(
            _selectedGameId,
            betAmount,
            _selectedTargetUserId,
            _isPracticeBotSelected);

        if (!result.Success || result.Challenge == null)
        {
            await DisplayAlert("Info", result.Message, "OK");
            return;
        }

        RefreshAllUI();
        await OpenMatchBoardAsync(result.Challenge.Id);
    }

    private async Task HandleAcceptChallengeAsync(string challengeId)
    {
        var result = await _appState.AcceptChallengeAsync(challengeId);
        if (!result.Success)
        {
            await DisplayAlert("Info", result.Message, "OK");
            return;
        }

        RefreshAllUI();
        await OpenMatchBoardAsync(challengeId);
    }

    private async Task OpenMatchBoardAsync(string challengeId)
    {
        var matchPage = new GameMatchPage(_appState, challengeId);
        await Navigation.PushAsync(matchPage);
    }

    private async void OnOpenProfileClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new ProfilePage(_appState));
    }

    private async void OnOpenWalletClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new WalletPage(_appState));
    }

    private async void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage(_appState));
    }

    private async void OnOpenAdminClicked(object? sender, EventArgs e)
    {
        if (_appState.CurrentUser?.IsAdmin != true)
            return;

        await Navigation.PushAsync(new AdminPage(_appState));
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        _appState.Logout();
        await Shell.Current.GoToAsync("//LoginPage");
    }
}

