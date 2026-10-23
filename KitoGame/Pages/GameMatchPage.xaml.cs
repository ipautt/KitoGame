using KitoGame.Engine;
using KitoGame.Engine.Rendering;
using KitoGame.Models;
using KitoGame.Services;

namespace KitoGame.Pages;

public partial class GameMatchPage : ContentPage
{
    private readonly AppStateService _appState;
    private readonly string _challengeId;
    private MatchChallenge? _challenge;
    private IGameEngine? _engine;

    private readonly BoardCanvasDrawable _boardDrawable = new();
    private BoardPosition? _selectedCell;
    private IReadOnlyList<BoardPosition> _highlightedMoves = Array.Empty<BoardPosition>();
    private PlayerSide _mySide = PlayerSide.Player1;
    private int _appliedMoveCount;
    private bool _isWaitingForBot;
    private bool _isSyncingCloud;
    private bool _isFinishingMatch;
    private IDispatcherTimer? _liveSyncTimer;

    public GameMatchPage(AppStateService appState, string challengeId)
    {
        InitializeComponent();
        _appState = appState;
        _challengeId = challengeId;

        BoardCanvasView.Drawable = _boardDrawable;
        LoadMatchAndInitializeBoard();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartLiveMatchSyncTimer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _liveSyncTimer?.Stop();
    }

    private void StartLiveMatchSyncTimer()
    {
        _liveSyncTimer?.Stop();
        if (_challenge != null && _challenge.IsPracticeBot)
            return;

        _liveSyncTimer = Dispatcher.CreateTimer();
        _liveSyncTimer.Interval = TimeSpan.FromMilliseconds(1000);
        _liveSyncTimer.Tick += async (_, _) =>
        {
            try
            {
                await SyncLiveMatchFromCloudAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncTimer] Tick Error: {ex.Message}");
            }
        };
        _liveSyncTimer.Start();
    }

    private async Task SyncLiveMatchFromCloudAsync()
    {
        if (_isSyncingCloud || _isFinishingMatch)
            return;

        _isSyncingCloud = true;
        try
        {
            var latest = await _appState.PullLiveChallengeStateAsync(_challengeId);
            if (latest == null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LblConnectionStatus.Text = "☁️ Local";
                    LblConnectionStatus.TextColor = Color.FromArgb("#64748B");
                });
                return;
            }

            _challenge = latest;

            if (_engine == null)
            {
                _engine = _appState.GameRegistry.GetOrCreateMatchEngine(_challenge.Id, _challenge.GameId);
                _engine.ResetBoard();
                _appliedMoveCount = 0;
                _boardDrawable.Engine = _engine;
            }

            bool hasNewMoves = _challenge.SerializedMoves.Count > _appliedMoveCount;
            if (hasNewMoves)
            {
                for (int i = _appliedMoveCount; i < _challenge.SerializedMoves.Count; i++)
                {
                    if (TryParseMoveToken(_challenge.SerializedMoves[i], out var from, out var to))
                    {
                        _engine.TryMakeMove(from, to);
                    }
                }
                _appliedMoveCount = _challenge.SerializedMoves.Count;
            }

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                LblConnectionStatus.Text = "🟢 En Vivo";
                LblConnectionStatus.TextColor = Color.FromArgb("#16A34A");

                UpdatePlayerRolesAndHeaders();

                if (hasNewMoves)
                {
                    _selectedCell = _engine.ForcedPiecePosition;
                    _highlightedMoves = _selectedCell.HasValue
                        ? _engine.GetValidMoves(_selectedCell.Value)
                        : Array.Empty<BoardPosition>();

                    LblHelperHint.Text = _engine.CurrentTurn == _mySide
                        ? "¡El contrincante realizó su jugada! Es tu turno."
                        : "El contrincante continúa su jugada...";
                }

                RefreshMatchUI();

                if ((_challenge.Status == MatchStatus.Completed || _challenge.Status == MatchStatus.Draw || _engine.IsGameOver) &&
                    !_isFinishingMatch)
                {
                    await FinalizeAndPayMatchAsync();
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SyncLiveMatch] Error: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LblConnectionStatus.Text = "⚠️ Error Sinc";
                LblConnectionStatus.TextColor = Color.FromArgb("#DC2626");
            });
        }
        finally
        {
            _isSyncingCloud = false;
        }
    }

    private async void OnManualSyncClicked(object? sender, EventArgs e)
    {
        BtnManualSync.IsEnabled = false;
        BtnManualSync.Text = "⏳...";
        try
        {
            var fresh = await _appState.MongoDb.GetChallengeByIdAsync(_challengeId);
            if (fresh != null)
            {
                _challenge = fresh;
                if (_engine != null)
                {
                    _engine.ResetBoard();
                    _appliedMoveCount = 0;
                    foreach (var m in _challenge.SerializedMoves)
                    {
                        if (TryParseMoveToken(m, out var f, out var t))
                        {
                            _engine.TryMakeMove(f, t);
                        }
                        _appliedMoveCount++;
                    }
                }
            }
            await SyncLiveMatchFromCloudAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OnManualSyncClicked] Error: {ex.Message}");
        }
        finally
        {
            BtnManualSync.IsEnabled = true;
            BtnManualSync.Text = "🔄 Refrescar";
        }
    }

    private static bool TryParseMoveToken(string token, out BoardPosition from, out BoardPosition to)
    {
        from = default;
        to = default;
        var parts = token.Split("->", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;

        var f = parts[0].Split(',');
        var t = parts[1].Split(',');
        if (f.Length != 2 || t.Length != 2) return false;

        if (int.TryParse(f[0], out int r1) && int.TryParse(f[1], out int c1) &&
            int.TryParse(t[0], out int r2) && int.TryParse(t[1], out int c2))
        {
            from = new BoardPosition(r1, c1);
            to = new BoardPosition(r2, c2);
            return true;
        }
        return false;
    }

    private async void LoadMatchAndInitializeBoard()
    {
        _challenge = _appState.Challenges.FirstOrDefault(c => c.Id == _challengeId);
        if (_challenge == null)
        {
            _challenge = await _appState.PullLiveChallengeStateAsync(_challengeId);
        }

        if (_challenge == null)
        {
            LblTurnBanner.Text = "Cargando datos de la partida...";
            return;
        }

        _engine = _appState.GameRegistry.GetOrCreateMatchEngine(_challenge.Id, _challenge.GameId);
        _engine.ResetBoard();
        _appliedMoveCount = 0;

        foreach (var moveToken in _challenge.SerializedMoves)
        {
            if (TryParseMoveToken(moveToken, out var from, out var to))
            {
                _engine.TryMakeMove(from, to);
            }
            _appliedMoveCount++;
        }

        _boardDrawable.Engine = _engine;
        Title = $"{_challenge.GameName} (#{_challenge.Id})";

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            UpdatePlayerRolesAndHeaders();

            if (_challenge.IsPracticeBot && !_engine.IsGameOver && _engine.CurrentTurn == PlayerSide.Player2)
            {
                await ExecutePracticeBotTurnAsync();
            }
            else if (_engine.ForcedPiecePosition.HasValue && _engine.CurrentTurn == _mySide)
            {
                _selectedCell = _engine.ForcedPiecePosition.Value;
                _highlightedMoves = _engine.GetValidMoves(_selectedCell.Value);
            }

            RefreshMatchUI();
        });

        StartLiveMatchSyncTimer();
    }

    private void UpdatePlayerRolesAndHeaders()
    {
        if (_challenge == null) return;

        string? myUserId = _appState.CurrentUser?.Id;
        if (_challenge.IsPracticeBot || myUserId == _challenge.CreatorId)
        {
            _mySide = PlayerSide.Player1;
        }
        else if (myUserId == _challenge.OpponentId)
        {
            _mySide = PlayerSide.Player2;
        }
        else
        {
            _mySide = PlayerSide.Player1;
        }

        string p1Name = string.IsNullOrWhiteSpace(_challenge.CreatorUsername) ? "Jugador 1" : _challenge.CreatorUsername;
        string p2Name = _challenge.Status == MatchStatus.WaitingForOpponent
            ? (string.IsNullOrWhiteSpace(_challenge.TargetOpponentUsername) ? "Esperando Jugador..." : $"{_challenge.TargetOpponentUsername} (Por aceptar)")
            : (string.IsNullOrWhiteSpace(_challenge.OpponentUsername) ? "Jugador 2" : _challenge.OpponentUsername);

        string p1Tag = _mySide == PlayerSide.Player1 ? $"TÚ ({p1Name.ToUpperInvariant()})" : p1Name.ToUpperInvariant();
        string p2Tag = _mySide == PlayerSide.Player2 ? $"TÚ ({p2Name.ToUpperInvariant()})" : p2Name.ToUpperInvariant();

        LblPlayer1Name.Text = _challenge.GameId == "checkers"
            ? $"🔴 {p1Tag}"
            : $"♔ {p1Tag}";

        LblOpponentName.Text = _challenge.GameId == "checkers"
            ? $"⚫ {p2Tag}"
            : $"♚ {p2Tag}";

        LblPlayer1Status.Text = _mySide == PlayerSide.Player1
            ? "🔴 Juegas con las fichas de abajo (Rojas / Blancas)"
            : $"🔴 Fichas de {p1Name} (Abajo)";
    }

    private void RefreshMatchUI()
    {
        if (_challenge == null || _engine == null)
            return;

        decimal commRate = _appState.CommissionPercentage;
        decimal commAmount = Math.Round(_challenge.TotalPot * (commRate / 100m), 0);
        decimal winnerPrize = _challenge.TotalPot - commAmount;

        LblMatchTitle.Text = $"{_challenge.GameIcon} {_challenge.GameName} • #{_challenge.Id}";
        LblPotBadge.Text = $"POZO: {_challenge.FormattedPot}";
        LblPrizeBreakdown.Text = commRate == 0
            ? $"Premio Neto al Ganador: ${winnerPrize:N0} COP (Comisión 0%)"
            : $"Premio Neto al Ganador: ${winnerPrize:N0} COP (Comisión {commRate:0.#}%: ${commAmount:N0} COP)";

        string unitLabel = _challenge.GameId == "checkers" ? "fichas" : "piezas";
        LblPlayer1Count.Text = $"{_engine.Player1PiecesCount} {unitLabel}";
        LblPlayer2Count.Text = $"{_engine.Player2PiecesCount} {unitLabel}";
        BtnAdminSimulateWin.IsVisible = _appState.CurrentUser?.IsAdmin == true;

        if (_challenge.Status == MatchStatus.WaitingForOpponent)
        {
            TurnBannerBorder.BackgroundColor = Color.FromArgb("#FFFBEB");
            TurnBannerBorder.Stroke = Color.FromArgb("#F59E0B");
            LblTurnBanner.TextColor = Color.FromArgb("#B45309");
            LblHelperHint.TextColor = Color.FromArgb("#92400E");

            LblTurnBanner.Text = "⏳ Esperando que tu contrincante acepte el reto...";
            LblHelperHint.Text = "Apenas tu amigo acepte el reto en su celular, el tablero se activará automáticamente.";
            LblOpponentStatus.Text = "⏳ Invitación enviada en MongoDB Atlas...";
        }
        else
        {
            bool isMyTurn = _engine.CurrentTurn == _mySide && !_isWaitingForBot;
            TurnBannerBorder.BackgroundColor = isMyTurn ? Color.FromArgb("#ECFDF5") : Color.FromArgb("#F3E8FF");
            TurnBannerBorder.Stroke = isMyTurn ? Color.FromArgb("#10B981") : Color.FromArgb("#8B5CF6");
            LblTurnBanner.TextColor = isMyTurn ? Color.FromArgb("#065F46") : Color.FromArgb("#5B21B6");
            LblHelperHint.TextColor = isMyTurn ? Color.FromArgb("#047857") : Color.FromArgb("#6D28D9");

            string myColorDesc = _mySide == PlayerSide.Player1
                ? (_challenge.GameId == "checkers" ? "Fichas Rojas (Abajo)" : "Piezas Blancas ♔ (Abajo)")
                : (_challenge.GameId == "checkers" ? "Fichas Oscuras/Moradas (Arriba)" : "Piezas Negras ♚ (Arriba)");

            string rivalName = _mySide == PlayerSide.Player1
                ? (_challenge.OpponentUsername ?? "Contrincante")
                : _challenge.CreatorUsername;

            LblTurnBanner.Text = isMyTurn
                ? $"🟢 ¡Es tu turno! Juegas con {myColorDesc}"
                : $"⏳ Turno de {rivalName}...";

            LblOpponentStatus.Text = isMyTurn
                ? "🟢 Conectado en línea • Esperando tu jugada"
                : $"🤔 {rivalName} está pensando su movimiento...";

            Player1CardBorder.BackgroundColor = _engine.CurrentTurn == PlayerSide.Player1 ? Color.FromArgb("#FFFBEB") : Color.FromArgb("#FFFFFF");
            OpponentCardBorder.BackgroundColor = _engine.CurrentTurn == PlayerSide.Player2 ? Color.FromArgb("#F3E8FF") : Color.FromArgb("#FFFFFF");
        }

        _boardDrawable.SelectedCell = _selectedCell;
        _boardDrawable.HighlightedMoves = _highlightedMoves;
        BoardCanvasView.Invalidate();

        if (_engine.MoveHistory.Count > 0)
        {
            LblMoveLog.Text = string.Join("\n", _engine.MoveHistory.Take(6));
        }
    }

    private async void OnBoardCanvasTouch(object? sender, TouchEventArgs e)
    {
        if (_engine == null || _challenge == null || _engine.IsGameOver || _isWaitingForBot || _isFinishingMatch)
            return;

        if (_challenge.Status == MatchStatus.WaitingForOpponent)
        {
            LblHelperHint.Text = "⏳ Espera a que tu contrincante acepte el reto en su celular para iniciar.";
            return;
        }

        // Bloqueo estricto por turno: solo puedes mover cuando el turno actual corresponde a tu lado (_mySide)
        if (_engine.CurrentTurn != _mySide)
        {
            LblHelperHint.Text = "⏳ Es el turno de tu contrincante. Espera a que realice su movimiento.";
            return;
        }

        var firstTouch = e.Touches.FirstOrDefault();
        var tappedPos = _boardDrawable.GetCellFromTouchPoint(
            firstTouch,
            (float)BoardCanvasView.Width,
            (float)BoardCanvasView.Height);

        if (!tappedPos.HasValue)
            return;

        var pos = tappedPos.Value;

        // 1. Si ya hay una ficha propia seleccionada y el usuario toca una casilla verde válida -> Mover
        if (_selectedCell.HasValue && _highlightedMoves.Contains(pos))
        {
            var fromCell = _selectedCell.Value;
            var moveResult = _engine.TryMakeMove(fromCell, pos);

            if (!moveResult.IsValid)
            {
                LblHelperHint.Text = moveResult.Message;
                RefreshMatchUI();
                return;
            }

            // Sincronizar jugada inmediatamente en MongoDB Atlas para que el otro celular la vea en vivo
            _appliedMoveCount++;
            bool pushOk = await _appState.RecordMoveAndSyncAsync(_challenge.Id, fromCell, pos);
            if (!pushOk)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LblConnectionStatus.Text = "⚠️ Reintentando...";
                    LblConnectionStatus.TextColor = Color.FromArgb("#D97706");
                });
            }

            if (_engine.IsGameOver)
            {
                _selectedCell = null;
                _highlightedMoves = Array.Empty<BoardPosition>();
                RefreshMatchUI();
                await FinalizeAndPayMatchAsync();
                return;
            }

            // Si el jugador tiene una captura doble en cadena con la misma ficha, la seleccionamos automáticamente
            if (moveResult.HasChainJump && moveResult.ChainJumpFrom.HasValue)
            {
                _selectedCell = moveResult.ChainJumpFrom.Value;
                _highlightedMoves = _engine.GetValidMoves(_selectedCell.Value);
                LblHelperHint.Text = "¡Doble salto disponible! Toca la casilla verde para completar la captura.";
                RefreshMatchUI();
                return;
            }

            _selectedCell = null;
            _highlightedMoves = Array.Empty<BoardPosition>();
            RefreshMatchUI();

            // Solo si es modo práctica contra el Bot, el Bot responde automáticamente
            if (_challenge.IsPracticeBot && _engine.CurrentTurn == PlayerSide.Player2 && !_engine.IsGameOver)
            {
                await ExecutePracticeBotTurnAsync();
            }
            return;
        }

        // 2. Si hay una ficha obligada por salto en cadena, no permitir cambiar de ficha
        if (_engine.ForcedPiecePosition.HasValue && pos != _engine.ForcedPiecePosition.Value)
        {
            _selectedCell = _engine.ForcedPiecePosition.Value;
            _highlightedMoves = _engine.GetValidMoves(_selectedCell.Value);
            LblHelperHint.Text = "Debes completar el salto doble con la ficha resaltada en dorado.";
            RefreshMatchUI();
            return;
        }

        // 3. Seleccionar una ficha/pieza propia (ÚNICAMENTE las que pertenecen a _mySide)
        var piece = _engine.GetPieceAt(pos);
        if (piece != null && piece.Owner == _mySide)
        {
            _selectedCell = pos;
            _highlightedMoves = _engine.GetValidMoves(pos);
            LblHelperHint.Text = _highlightedMoves.Count > 0
                ? $"Ficha seleccionada. Toca una de las {_highlightedMoves.Count} casillas con círculo verde para mover."
                : "Esa ficha está bloqueada. Toca otra de tus fichas.";
            RefreshMatchUI();
        }
        else if (piece != null && piece.Owner != _mySide)
        {
            LblHelperHint.Text = "⚠️ Esa ficha pertenece a tu contrincante. Toca una de tus propias fichas.";
            RefreshMatchUI();
        }
        else
        {
            _selectedCell = null;
            _highlightedMoves = Array.Empty<BoardPosition>();
            RefreshMatchUI();
        }
    }

    private async Task ExecutePracticeBotTurnAsync()
    {
        if (_engine == null || _engine.IsGameOver)
            return;

        _isWaitingForBot = true;
        RefreshMatchUI();

        await Task.Delay(550);

        _engine.TryMakeAutoMove();
        _isWaitingForBot = false;
        LblHelperHint.Text = "El Bot movió (casillas azules). Es tu turno.";
        RefreshMatchUI();

        if (_engine.IsGameOver)
        {
            await FinalizeAndPayMatchAsync();
        }
    }

    private async void OnDrawClicked(object? sender, EventArgs e)
    {
        if (_engine == null || _engine.IsGameOver || _isWaitingForBot || _isFinishingMatch)
            return;

        bool confirm = await DisplayAlert(
            "Declarar Tablas (Empate)",
            "¿Acordar empate y reembolsar las apuestas en custodia a cada jugador?",
            "Sí, Empate",
            "Cancelar");

        if (!confirm) return;

        _engine.DeclareDraw();
        RefreshMatchUI();
        await FinalizeAndPayMatchAsync();
    }

    private async void OnResignClicked(object? sender, EventArgs e)
    {
        if (_engine == null || _engine.IsGameOver || _isWaitingForBot || _isFinishingMatch)
            return;

        bool confirm = await DisplayAlert(
            "Rendirse",
            "¿Seguro que deseas rendirte? Tu contrincante ganará la partida y el pozo.",
            "Sí, Rendirme",
            "Seguir Jugando");

        if (!confirm) return;

        _engine.Resign(_mySide);
        RefreshMatchUI();
        await FinalizeAndPayMatchAsync();
    }

    private async void OnSimulateWinP1Clicked(object? sender, EventArgs e)
    {
        if (_engine == null || _engine.IsGameOver || _isWaitingForBot || _isFinishingMatch)
            return;

        PlayerSide loserSide = _mySide == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
        _engine.Resign(loserSide);
        RefreshMatchUI();
        await FinalizeAndPayMatchAsync();
    }

    private async Task FinalizeAndPayMatchAsync()
    {
        if (_challenge == null || _engine == null || _isFinishingMatch)
            return;

        _isFinishingMatch = true;
        _liveSyncTimer?.Stop();

        PlayerSide winner = _engine.Winner;
        bool isDraw = _engine.IsDraw;
        if (!_engine.IsGameOver && (_challenge.Status == MatchStatus.Completed || _challenge.Status == MatchStatus.Draw))
        {
            isDraw = _challenge.Status == MatchStatus.Draw;
            winner = _challenge.WinnerUserId == _challenge.CreatorId ? PlayerSide.Player1 : PlayerSide.Player2;
        }

        var settlement = _appState.SettleMatchResult(_challenge.Id, winner, isDraw);
        await DisplayAlert("🏆 Partida Finalizada y Liquidada", settlement.SummaryMessage, "Volver al Lobby");
        await Navigation.PopAsync();
    }
}

