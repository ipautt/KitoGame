using KitoGame.Models;

namespace KitoGame.Engine.Chess;

public class ChessEngine : IGameEngine
{
    private readonly BoardPiece?[,] _board = new BoardPiece?[8, 8];

    public GameMetadata Metadata { get; } = new()
    {
        Id = "chess",
        Name = "Ajedrez Competitivo",
        Icon = "♟️",
        ShortDescription = "El clásico rey de la estrategia 1v1. Captura al Rey rival para llevarte el pozo.",
        RulesSummary = "Reglas estándar de movimiento por pieza, coronación automática a Reina en la octava fila y victoria al capturar el Rey o por rendición.",
        MinBet = 1000m,
        AccentColorHex = "#3B82F6"
    };

    public int Rows => 8;
    public int Cols => 8;
    public PlayerSide CurrentTurn { get; private set; } = PlayerSide.Player1;
    public bool IsGameOver { get; private set; }
    public PlayerSide Winner { get; private set; } = PlayerSide.None;
    public bool IsDraw { get; private set; }
    public string StatusMessage { get; private set; } = "Tu turno (Piezas Blancas ♔)";
    public List<string> MoveHistory { get; } = new();

    public BoardPosition? LastMoveFrom { get; private set; }
    public BoardPosition? LastMoveTo { get; private set; }
    public BoardPosition? ForcedPiecePosition => null;
    public int Player1PiecesCount { get; private set; } = 16;
    public int Player2PiecesCount { get; private set; } = 16;

    public ChessEngine()
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
        LastMoveFrom = null;
        LastMoveTo = null;
        Player1PiecesCount = 16;
        Player2PiecesCount = 16;
        StatusMessage = "Tu turno (Piezas Blancas ♔)";

        string[] backRankCodes = { "R", "N", "B", "Q", "K", "B", "N", "R" };
        string[] blackSymbols = { "♜", "♞", "♝", "♛", "♚", "♝", "♞", "♜" };
        string[] whiteSymbols = { "♖", "♘", "♗", "♕", "♔", "♗", "♘", "♖" };

        for (int c = 0; c < 8; c++)
        {
            _board[0, c] = new BoardPiece { Owner = PlayerSide.Player2, Code = backRankCodes[c], Symbol = blackSymbols[c] };
            _board[1, c] = new BoardPiece { Owner = PlayerSide.Player2, Code = "P", Symbol = "♟" };

            _board[6, c] = new BoardPiece { Owner = PlayerSide.Player1, Code = "P", Symbol = "♙" };
            _board[7, c] = new BoardPiece { Owner = PlayerSide.Player1, Code = backRankCodes[c], Symbol = whiteSymbols[c] };
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

        switch (piece.Code)
        {
            case "P":
                AddPawnMoves(from, piece.Owner, moves);
                break;
            case "R":
                AddSlidingMoves(from, piece.Owner, moves, new[] { (-1, 0), (1, 0), (0, -1), (0, 1) });
                break;
            case "B":
                AddSlidingMoves(from, piece.Owner, moves, new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) });
                break;
            case "Q":
                AddSlidingMoves(from, piece.Owner, moves, new[]
                {
                    (-1, 0), (1, 0), (0, -1), (0, 1),
                    (-1, -1), (-1, 1), (1, -1), (1, 1)
                });
                break;
            case "N":
                int[,] jumps = { { -2, -1 }, { -2, 1 }, { -1, -2 }, { -1, 2 }, { 1, -2 }, { 1, 2 }, { 2, -1 }, { 2, 1 } };
                for (int i = 0; i < jumps.GetLength(0); i++)
                {
                    var target = new BoardPosition(from.Row + jumps[i, 0], from.Col + jumps[i, 1]);
                    if (target.IsInsideBoard())
                    {
                        var occ = _board[target.Row, target.Col];
                        if (occ == null || occ.Owner != piece.Owner)
                            moves.Add(target);
                    }
                }
                break;
            case "K":
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        var target = new BoardPosition(from.Row + dr, from.Col + dc);
                        if (target.IsInsideBoard())
                        {
                            var occ = _board[target.Row, target.Col];
                            if (occ == null || occ.Owner != piece.Owner)
                                moves.Add(target);
                        }
                    }
                }
                break;
        }

        return moves;
    }

    public MoveResult TryMakeMove(BoardPosition from, BoardPosition to)
    {
        if (IsGameOver)
            return new MoveResult { IsValid = false, Message = "La partida ya finalizó." };

        var piece = GetPieceAt(from);
        if (piece == null || piece.Owner != CurrentTurn)
            return new MoveResult { IsValid = false, Message = "Selecciona una pieza de tu color." };

        var validMoves = GetValidMoves(from);
        if (!validMoves.Contains(to))
            return new MoveResult { IsValid = false, Message = "Movimiento ilegal para esa pieza." };

        var targetPiece = _board[to.Row, to.Col];
        bool captured = targetPiece != null;
        bool capturedKing = targetPiece?.Code == "K";

        _board[to.Row, to.Col] = piece;
        _board[from.Row, from.Col] = null;

        LastMoveFrom = from;
        LastMoveTo = to;

        if (piece.Code == "P")
        {
            if ((piece.Owner == PlayerSide.Player1 && to.Row == 0) ||
                (piece.Owner == PlayerSide.Player2 && to.Row == 7))
            {
                piece.Code = "Q";
                piece.IsKingOrPromoted = true;
                piece.Symbol = piece.Owner == PlayerSide.Player1 ? "♕" : "♛";
            }
        }

        RecountPieces();

        string coordFrom = $"{(char)('A' + from.Col)}{8 - from.Row}";
        string coordTo = $"{(char)('A' + to.Col)}{8 - to.Row}";
        string actor = CurrentTurn == PlayerSide.Player1 ? "Tú" : "Rival";
        MoveHistory.Insert(0, $"{actor} ({piece.Symbol}): {coordFrom} {(captured ? "x" : "→")} {coordTo}");

        if (capturedKing)
        {
            IsGameOver = true;
            Winner = CurrentTurn;
            StatusMessage = Winner == PlayerSide.Player1
                ? "¡Rey capturado! Ganaste la partida."
                : "El contrincante capturó tu Rey.";
            return new MoveResult
            {
                IsValid = true,
                CapturedPiece = true,
                IsGameOver = true,
                Winner = Winner,
                Message = StatusMessage
            };
        }

        CurrentTurn = CurrentTurn == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
        StatusMessage = CurrentTurn == PlayerSide.Player1
            ? "Tu turno (Piezas Blancas ♔)"
            : "Turno del contrincante (Piezas Negras ♚)";

        return new MoveResult
        {
            IsValid = true,
            CapturedPiece = captured,
            IsGameOver = IsGameOver,
            Winner = Winner,
            Message = StatusMessage
        };
    }

    public MoveResult TryMakeAutoMove()
    {
        if (IsGameOver)
            return new MoveResult { IsValid = false, Message = "Partida terminada." };

        var allMoves = new List<(BoardPosition From, BoardPosition To, int Score)>();
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
                        var target = _board[dst.Row, dst.Col];
                        int score = target?.Code switch
                        {
                            "K" => 1000,
                            "Q" => 90,
                            "R" => 50,
                            "B" or "N" => 30,
                            "P" => 10,
                            _ => 1
                        };
                        allMoves.Add((pos, dst, score));
                    }
                }
            }
        }

        if (allMoves.Count == 0)
        {
            DeclareDraw();
            return new MoveResult { IsValid = false, IsGameOver = true, IsDraw = true, Message = StatusMessage };
        }

        int maxScore = allMoves.Max(m => m.Score);
        var bestCandidates = allMoves.Where(m => m.Score == maxScore).ToList();
        var chosen = bestCandidates[Random.Shared.Next(bestCandidates.Count)];

        return TryMakeMove(chosen.From, chosen.To);
    }

    public void Resign(PlayerSide resigningSide)
    {
        if (IsGameOver) return;
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
        IsGameOver = true;
        IsDraw = true;
        Winner = PlayerSide.None;
        StatusMessage = "Partida acordada en Tablas (Empate).";
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

    private void AddPawnMoves(BoardPosition from, PlayerSide owner, List<BoardPosition> moves)
    {
        int dir = owner == PlayerSide.Player1 ? -1 : 1;
        int startRow = owner == PlayerSide.Player1 ? 6 : 1;

        var oneStep = new BoardPosition(from.Row + dir, from.Col);
        if (oneStep.IsInsideBoard() && _board[oneStep.Row, oneStep.Col] == null)
        {
            moves.Add(oneStep);
            var twoStep = new BoardPosition(from.Row + dir * 2, from.Col);
            if (from.Row == startRow && twoStep.IsInsideBoard() && _board[twoStep.Row, twoStep.Col] == null)
            {
                moves.Add(twoStep);
            }
        }

        foreach (int dc in new[] { -1, 1 })
        {
            var diag = new BoardPosition(from.Row + dir, from.Col + dc);
            if (diag.IsInsideBoard())
            {
                var occ = _board[diag.Row, diag.Col];
                if (occ != null && occ.Owner != owner)
                {
                    moves.Add(diag);
                }
            }
        }
    }

    private void AddSlidingMoves(BoardPosition from, PlayerSide owner, List<BoardPosition> moves, (int dr, int dc)[] dirs)
    {
        foreach (var (dr, dc) in dirs)
        {
            int r = from.Row + dr;
            int c = from.Col + dc;
            while (r >= 0 && r < 8 && c >= 0 && c < 8)
            {
                var occ = _board[r, c];
                if (occ == null)
                {
                    moves.Add(new BoardPosition(r, c));
                }
                else
                {
                    if (occ.Owner != owner)
                        moves.Add(new BoardPosition(r, c));
                    break;
                }
                r += dr;
                c += dc;
            }
        }
    }
}

