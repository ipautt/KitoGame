using KitoGame.Models;

namespace KitoGame.Engine.Checkers;

public class CheckersEngine : IGameEngine
{
    private readonly BoardPiece?[,] _board = new BoardPiece?[8, 8];
    private BoardPosition? _chainJumpPosition;

    public GameMetadata Metadata { get; } = new()
    {
        Id = "checkers",
        Name = "Damas Clásicas",
        Icon = "🔴",
        ShortDescription = "Duelo 1v1 en tablero 8x8. Captura todas las fichas rivales o bloquéalas.",
        RulesSummary = "Movimiento diagonal en casillas oscuras. Saltos para capturar fichas rivales y coronación a Dama al llegar al extremo opuesto.",
        MinBet = 1000m,
        AccentColorHex = "#EF4444"
    };

    public int Rows => 8;
    public int Cols => 8;
    public PlayerSide CurrentTurn { get; private set; } = PlayerSide.Player1;
    public bool IsGameOver { get; private set; }
    public PlayerSide Winner { get; private set; } = PlayerSide.None;
    public bool IsDraw { get; private set; }
    public string StatusMessage { get; private set; } = "Tu turno (Fichas Rojas)";
    public List<string> MoveHistory { get; } = new();

    public BoardPosition? LastMoveFrom { get; private set; }
    public BoardPosition? LastMoveTo { get; private set; }
    public BoardPosition? ForcedPiecePosition => _chainJumpPosition;
    public int Player1PiecesCount { get; private set; } = 12;
    public int Player2PiecesCount { get; private set; } = 12;

    public CheckersEngine()
    {
        ResetBoard();
    }

    public void ResetBoard()
    {
        Array.Clear(_board, 0, _board.Length);
        MoveHistory.Clear();
        IsGameOver = false;
        IsDraw = false;
        Winner = PlayerSide.None;
        CurrentTurn = PlayerSide.Player1;
        _chainJumpPosition = null;
        LastMoveFrom = null;
        LastMoveTo = null;
        Player1PiecesCount = 12;
        Player2PiecesCount = 12;
        StatusMessage = "Tu turno (Fichas Rojas)";

        // Contrincante (Jugador 2 - Arriba: filas 0, 1, 2)
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                if ((r + c) % 2 == 1)
                {
                    _board[r, c] = new BoardPiece
                    {
                        Owner = PlayerSide.Player2,
                        Code = "MAN",
                        Symbol = "⚫",
                        IsKingOrPromoted = false
                    };
                }
            }
        }

        // Usuario local (Jugador 1 - Abajo: filas 5, 6, 7)
        for (int r = 5; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                if ((r + c) % 2 == 1)
                {
                    _board[r, c] = new BoardPiece
                    {
                        Owner = PlayerSide.Player1,
                        Code = "MAN",
                        Symbol = "🔴",
                        IsKingOrPromoted = false
                    };
                }
            }
        }
    }

    public BoardPiece? GetPieceAt(BoardPosition position) =>
        position.IsInsideBoard(Rows, Cols) ? _board[position.Row, position.Col] : null;

    public IReadOnlyList<BoardPosition> GetValidMoves(BoardPosition from)
    {
        var moves = new List<BoardPosition>();
        if (IsGameOver || !from.IsInsideBoard())
            return moves;

        var piece = _board[from.Row, from.Col];
        if (piece == null || piece.Owner != CurrentTurn)
            return moves;

        // Si estamos en medio de un salto múltiple (doble captura), solo esa misma ficha puede moverse y solo para saltar
        if (_chainJumpPosition.HasValue)
        {
            if (from != _chainJumpPosition.Value)
                return moves;

            AddJumpMovesForPiece(from, piece, moves);
            return moves;
        }

        int[] rowDirs = piece.IsKingOrPromoted
            ? new[] { -1, 1 }
            : (piece.Owner == PlayerSide.Player1 ? new[] { -1 } : new[] { 1 });

        int[] colDirs = { -1, 1 };

        // Primero agregamos saltos de captura
        AddJumpMovesForPiece(from, piece, moves);

        // Luego agregamos pasos diagonales normales
        foreach (int dr in rowDirs)
        {
            foreach (int dc in colDirs)
            {
                var step = new BoardPosition(from.Row + dr, from.Col + dc);
                if (step.IsInsideBoard() && _board[step.Row, step.Col] == null)
                {
                    moves.Add(step);
                }
            }
        }

        return moves;
    }

    private void AddJumpMovesForPiece(BoardPosition from, BoardPiece piece, List<BoardPosition> moves)
    {
        int[] rowDirs = piece.IsKingOrPromoted
            ? new[] { -1, 1 }
            : (piece.Owner == PlayerSide.Player1 ? new[] { -1 } : new[] { 1 });

        int[] colDirs = { -1, 1 };

        foreach (int dr in rowDirs)
        {
            foreach (int dc in colDirs)
            {
                var step = new BoardPosition(from.Row + dr, from.Col + dc);
                var jump = new BoardPosition(from.Row + dr * 2, from.Col + dc * 2);
                if (step.IsInsideBoard() && jump.IsInsideBoard())
                {
                    var midPiece = _board[step.Row, step.Col];
                    if (midPiece != null && midPiece.Owner != piece.Owner && _board[jump.Row, jump.Col] == null)
                    {
                        moves.Add(jump);
                    }
                }
            }
        }
    }

    public MoveResult TryMakeMove(BoardPosition from, BoardPosition to)
    {
        if (IsGameOver)
        {
            return new MoveResult { IsValid = false, Message = "La partida ya terminó." };
        }

        var piece = GetPieceAt(from);
        if (piece == null || piece.Owner != CurrentTurn)
        {
            return new MoveResult { IsValid = false, Message = "Selecciona una de tus fichas." };
        }

        var validMoves = GetValidMoves(from);
        if (!validMoves.Contains(to))
        {
            return new MoveResult { IsValid = false, Message = "Movimiento diagonal no válido." };
        }

        bool captured = false;
        int rowDiff = to.Row - from.Row;
        int colDiff = to.Col - from.Col;

        _board[to.Row, to.Col] = piece;
        _board[from.Row, from.Col] = null;

        if (Math.Abs(rowDiff) == 2 && Math.Abs(colDiff) == 2)
        {
            int midRow = from.Row + rowDiff / 2;
            int midCol = from.Col + colDiff / 2;
            _board[midRow, midCol] = null;
            captured = true;
        }

        LastMoveFrom = from;
        LastMoveTo = to;

        // Coronación a Reina (Dama)
        bool justCrowned = false;
        if (!piece.IsKingOrPromoted)
        {
            if ((piece.Owner == PlayerSide.Player1 && to.Row == 0) ||
                (piece.Owner == PlayerSide.Player2 && to.Row == 7))
            {
                piece.IsKingOrPromoted = true;
                piece.Code = "KING";
                piece.Symbol = piece.Owner == PlayerSide.Player1 ? "👑" : "⚜️";
                justCrowned = true;
            }
        }

        string coordFrom = $"{(char)('A' + from.Col)}{8 - from.Row}";
        string coordTo = $"{(char)('A' + to.Col)}{8 - to.Row}";
        string actor = CurrentTurn == PlayerSide.Player1 ? "Tú" : "Rival";
        MoveHistory.Insert(0, $"{actor}: {coordFrom} {(captured ? "x (Captura)" : "→")} {coordTo}{(justCrowned ? " 👑" : "")}");

        RecountPieces();

        // Si hubo captura, no acaba de coronar, y tiene otro salto inmediato con la misma ficha:
        if (captured && !justCrowned)
        {
            var nextJumps = new List<BoardPosition>();
            AddJumpMovesForPiece(to, piece, nextJumps);
            if (nextJumps.Count > 0)
            {
                _chainJumpPosition = to;
                StatusMessage = CurrentTurn == PlayerSide.Player1
                    ? "¡Captura doble disponible! Mueve la misma ficha."
                    : "El rival está realizando un salto múltiple...";

                return new MoveResult
                {
                    IsValid = true,
                    CapturedPiece = true,
                    HasChainJump = true,
                    ChainJumpFrom = to,
                    Message = StatusMessage
                };
            }
        }

        // Termina el turno actual y pasa al siguiente jugador
        _chainJumpPosition = null;
        CurrentTurn = CurrentTurn == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
        CheckEndOfGame();

        return new MoveResult
        {
            IsValid = true,
            CapturedPiece = captured,
            HasChainJump = false,
            IsGameOver = IsGameOver,
            Winner = Winner,
            IsDraw = IsDraw,
            Message = StatusMessage
        };
    }

    public MoveResult TryMakeAutoMove()
    {
        if (IsGameOver)
            return new MoveResult { IsValid = false, Message = "Partida finalizada." };

        var startingTurn = CurrentTurn;
        MoveResult lastResult = new() { IsValid = false, Message = "Sin movimientos." };
        int safetyCounter = 0;

        // Ejecuta el movimiento (y si tiene salto doble/triple, completa todos los saltos del mismo turno)
        while (!IsGameOver && CurrentTurn == startingTurn && safetyCounter++ < 12)
        {
            var allMoves = new List<(BoardPosition From, BoardPosition To, bool IsCapture)>();

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    var pos = new BoardPosition(r, c);
                    var p = _board[r, c];
                    if (p != null && p.Owner == CurrentTurn)
                    {
                        foreach (var dst in GetValidMoves(pos))
                        {
                            bool isCap = Math.Abs(dst.Row - pos.Row) == 2;
                            allMoves.Add((pos, dst, isCap));
                        }
                    }
                }
            }

            if (allMoves.Count == 0)
            {
                _chainJumpPosition = null;
                CheckEndOfGame();
                return new MoveResult { IsValid = false, IsGameOver = IsGameOver, Winner = Winner, Message = StatusMessage };
            }

            var captures = allMoves.Where(m => m.IsCapture).ToList();
            var chosen = captures.Count > 0
                ? captures[Random.Shared.Next(captures.Count)]
                : allMoves[Random.Shared.Next(allMoves.Count)];

            lastResult = TryMakeMove(chosen.From, chosen.To);
            if (!lastResult.IsValid)
            {
                _chainJumpPosition = null;
                break;
            }
        }

        return lastResult;
    }

    public void Resign(PlayerSide resigningSide)
    {
        if (IsGameOver) return;
        _chainJumpPosition = null;
        IsGameOver = true;
        Winner = resigningSide == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
        StatusMessage = Winner == PlayerSide.Player1
            ? "¡Tu rival se rindió! Ganaste la partida."
            : "Te has rendido. Gana el contrincante.";
        MoveHistory.Insert(0, $"🏳️ Jugador {(int)resigningSide} se rindió.");
    }

    public void DeclareDraw()
    {
        if (IsGameOver) return;
        _chainJumpPosition = null;
        IsGameOver = true;
        IsDraw = true;
        Winner = PlayerSide.None;
        StatusMessage = "Partida declarada en Tablas (Empate).";
        MoveHistory.Insert(0, "🤝 Acuerdo de Tablas (Empate).");
    }

    private void RecountPieces()
    {
        int p1 = 0, p2 = 0;
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                var p = _board[r, c];
                if (p?.Owner == PlayerSide.Player1) p1++;
                else if (p?.Owner == PlayerSide.Player2) p2++;
            }
        }
        Player1PiecesCount = p1;
        Player2PiecesCount = p2;
    }

    private void CheckEndOfGame()
    {
        RecountPieces();
        bool currentPlayerHasMoves = false;

        for (int r = 0; r < Rows && !currentPlayerHasMoves; r++)
        {
            for (int c = 0; c < Cols && !currentPlayerHasMoves; c++)
            {
                var p = _board[r, c];
                if (p != null && p.Owner == CurrentTurn)
                {
                    if (GetValidMoves(new BoardPosition(r, c)).Count > 0)
                        currentPlayerHasMoves = true;
                }
            }
        }

        if (Player1PiecesCount == 0)
        {
            IsGameOver = true;
            Winner = PlayerSide.Player2;
            StatusMessage = "Fin del juego: El contrincante capturó todas tus fichas.";
        }
        else if (Player2PiecesCount == 0)
        {
            IsGameOver = true;
            Winner = PlayerSide.Player1;
            StatusMessage = "¡Victoria! Capturaste todas las fichas del contrincante.";
        }
        else if (!currentPlayerHasMoves)
        {
            IsGameOver = true;
            Winner = CurrentTurn == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
            StatusMessage = Winner == PlayerSide.Player1
                ? "¡Victoria! Dejaste al contrincante bloqueado sin movimientos."
                : "Sin movimientos disponibles. Gana el contrincante.";
        }
        else
        {
            StatusMessage = CurrentTurn == PlayerSide.Player1
                ? "Tu turno (Fichas Rojas)"
                : "Turno del contrincante (Fichas Negras)";
        }
    }
}

