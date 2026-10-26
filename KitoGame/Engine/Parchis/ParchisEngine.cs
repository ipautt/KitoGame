using KitoGame.Models;

namespace KitoGame.Engine.Parchis;

public enum ParchisPlayerColor
{
    Red = 1,    // Jugador 1 (Abajo Izquierda)
    Green = 2,  // Jugador 2 (Arriba Derecha o Rival 1v1)
    Yellow = 3, // Jugador 3 (Arriba Izquierda)
    Blue = 4    // Jugador 4 (Abajo Derecha)
}

public class ParchisToken
{
    public int Id { get; set; }
    public PlayerSide Owner { get; set; }
    public ParchisPlayerColor Color { get; set; }
    public int StepIndex { get; set; } = -1; // -1: En Cárcel/Casa, 0..67: En Pista, 68..75: Pasillo a Meta, 76: En Meta
    public bool IsInJail => StepIndex == -1;
    public bool IsInGoal => StepIndex >= 76;
    public BoardPosition Position { get; set; }
}

public class ParchisEngine : IGameEngine
{
    public GameMetadata Metadata { get; } = new()
    {
        Id = "parchis",
        Name = "Parchís",
        Icon = "🎲",
        ShortDescription = "El clásico juego de mesa para 2 a 4 jugadores. Lanza el dado y lleva tus 4 fichas a la meta.",
        RulesSummary = "Lanza el dado (1-6). Saca 5 para liberar ficha de la cárcel. Come fichas rivales para avanzar 20 casillas extra. Gana quien corone sus 4 fichas.",
        MinBet = 1000m,
        AccentColorHex = "#F59E0B"
    };

    public int Rows => 8;
    public int Cols => 8;

    public PlayerSide CurrentTurn { get; private set; } = PlayerSide.Player1;
    public bool IsGameOver { get; private set; }
    public PlayerSide Winner { get; private set; } = PlayerSide.None;
    public bool IsDraw { get; private set; }
    public string StatusMessage { get; private set; } = "Tu turno en Parchís. Toca el dado o una de tus fichas.";
    public List<string> MoveHistory { get; } = new();

    public BoardPosition? LastMoveFrom { get; private set; }
    public BoardPosition? LastMoveTo { get; private set; }
    public BoardPosition? ForcedPiecePosition { get; private set; }

    public int Player1PiecesCount => _tokens.Count(t => t.Owner == PlayerSide.Player1 && !t.IsInGoal);
    public int Player2PiecesCount => _tokens.Count(t => t.Owner == PlayerSide.Player2 && !t.IsInGoal);

    public int CurrentDiceValue { get; private set; } = 5;
    public bool HasRolledThisTurn { get; private set; } = false;

    private readonly List<ParchisToken> _tokens = new();
    private readonly Random _random = new();

    // Posiciones predeterminadas de las casas (Cárcel) en el tablero 8x8
    private static readonly BoardPosition[] P1JailSlots = { new(6, 1), new(6, 2), new(7, 1), new(7, 2) };
    private static readonly BoardPosition[] P2JailSlots = { new(0, 5), new(0, 6), new(1, 5), new(1, 6) };

    public ParchisEngine()
    {
        ResetBoard();
    }

    public void ResetBoard()
    {
        _tokens.Clear();
        MoveHistory.Clear();
        IsGameOver = false;
        IsDraw = false;
        Winner = PlayerSide.None;
        CurrentTurn = PlayerSide.Player1;
        LastMoveFrom = null;
        LastMoveTo = null;
        ForcedPiecePosition = null;
        HasRolledThisTurn = true;
        CurrentDiceValue = 5; // Dado inicial de cortesía para permitir salida rápida
        StatusMessage = "¡Partida iniciada! Toca una ficha roja en tu cárcel para sacarla con el 5.";

        // 4 Fichas Jugador 1 (Rojas - Abajo)
        for (int i = 0; i < 4; i++)
        {
            _tokens.Add(new ParchisToken
            {
                Id = i + 1,
                Owner = PlayerSide.Player1,
                Color = ParchisPlayerColor.Red,
                StepIndex = -1,
                Position = P1JailSlots[i]
            });
        }

        // 4 Fichas Jugador 2 (Verdes/Doradas - Arriba)
        for (int i = 0; i < 4; i++)
        {
            _tokens.Add(new ParchisToken
            {
                Id = i + 5,
                Owner = PlayerSide.Player2,
                Color = ParchisPlayerColor.Green,
                StepIndex = -1,
                Position = P2JailSlots[i]
            });
        }
    }

    public int RollDice()
    {
        CurrentDiceValue = _random.Next(1, 7);
        HasRolledThisTurn = true;
        StatusMessage = $"Dado: {CurrentDiceValue} 🎲 • {(CurrentTurn == PlayerSide.Player1 ? "Tu turno" : "Turno del rival")}";
        return CurrentDiceValue;
    }

    public BoardPiece? GetPieceAt(BoardPosition position)
    {
        var token = _tokens.FirstOrDefault(t => t.Position == position && !t.IsInGoal);
        if (token == null) return null;

        return new BoardPiece
        {
            Owner = token.Owner,
            Code = $"PARCHIS_{token.Id}",
            Symbol = token.Owner == PlayerSide.Player1 ? "🔴" : "🟢",
            IsKingOrPromoted = token.IsInGoal
        };
    }

    public IReadOnlyList<BoardPosition> GetValidMoves(BoardPosition from)
    {
        var moves = new List<BoardPosition>();
        if (IsGameOver) return moves;

        var token = _tokens.FirstOrDefault(t => t.Position == from && t.Owner == CurrentTurn && !t.IsInGoal);
        if (token == null) return moves;

        // Si está en cárcel, solo puede salir si el dado es 5
        if (token.IsInJail)
        {
            if (CurrentDiceValue == 5)
            {
                var startSquare = token.Owner == PlayerSide.Player1 ? new BoardPosition(5, 0) : new BoardPosition(2, 7);
                moves.Add(startSquare);
            }
            return moves;
        }

        // Si ya está en pista o pasillo, avanza los pasos del dado
        var dest = CalculateDestination(token, CurrentDiceValue);
        if (dest.HasValue)
        {
            moves.Add(dest.Value);
        }

        return moves;
    }

    private BoardPosition? CalculateDestination(ParchisToken token, int steps)
    {
        int targetStep = token.StepIndex + steps;
        if (targetStep > 76) return null; // No puede pasarse de la meta

        if (targetStep == 76)
        {
            // Casilla de Meta central
            return new BoardPosition(3, 4);
        }

        // Simulación de recorrido por el perímetro del tablero 8x8 (68 casillas mapeadas a la cuadrícula)
        return MapTrackStepToBoard(targetStep, token.Owner);
    }

    private static BoardPosition MapTrackStepToBoard(int step, PlayerSide owner)
    {
        // Ruta perimetral de 24 casillas exteriores + 8 interiores hacia la meta
        int mod = step % 24;
        if (mod < 7) return new BoardPosition(7 - mod, 0);
        if (mod < 14) return new BoardPosition(0, mod - 7);
        if (mod < 20) return new BoardPosition(mod - 14, 7);
        return new BoardPosition(7, 24 - mod);
    }

    public MoveResult TryMakeMove(BoardPosition from, BoardPosition to)
    {
        if (IsGameOver)
            return new MoveResult { IsValid = false, Message = "La partida ya terminó." };

        var token = _tokens.FirstOrDefault(t => t.Position == from && t.Owner == CurrentTurn && !t.IsInGoal);
        if (token == null)
            return new MoveResult { IsValid = false, Message = "Selecciona una de tus fichas." };

        var valids = GetValidMoves(from);
        if (!valids.Contains(to))
            return new MoveResult { IsValid = false, Message = "Movimiento no permitido para el valor del dado." };

        bool captured = false;

        // Si sale de cárcel
        if (token.IsInJail)
        {
            token.StepIndex = 0;
            token.Position = to;
            MoveHistory.Insert(0, $"{(CurrentTurn == PlayerSide.Player1 ? "Tú" : "Rival")}: Sale ficha #{token.Id} con dado 5 🎲");
        }
        else
        {
            token.StepIndex += CurrentDiceValue;
            token.Position = to;

            // Verificar si capturó ficha rival
            var opponentToken = _tokens.FirstOrDefault(t => t.Position == to && t.Owner != CurrentTurn && !t.IsInJail && !t.IsInGoal);
            if (opponentToken != null)
            {
                // Enviar ficha rival a la cárcel
                opponentToken.StepIndex = -1;
                opponentToken.Position = opponentToken.Owner == PlayerSide.Player1
                    ? P1JailSlots[opponentToken.Id - 1]
                    : P2JailSlots[opponentToken.Id - 5];

                captured = true;
                MoveHistory.Insert(0, $"⚔️ ¡Captura! Ficha #{opponentToken.Id} enviada a la cárcel (+20 pasos)");
            }

            if (token.IsInGoal)
            {
                MoveHistory.Insert(0, $"🏆 ¡Ficha #{token.Id} coronó en la Meta!");
            }
            else
            {
                MoveHistory.Insert(0, $"{(CurrentTurn == PlayerSide.Player1 ? "Tú" : "Rival")}: Avanza {CurrentDiceValue} casillas");
            }
        }

        LastMoveFrom = from;
        LastMoveTo = to;

        // Comprobar victoria (las 4 fichas en la meta)
        if (_tokens.Where(t => t.Owner == CurrentTurn).All(t => t.IsInGoal))
        {
            IsGameOver = true;
            Winner = CurrentTurn;
            StatusMessage = $"¡Victoria! {(Winner == PlayerSide.Player1 ? "Jugador 1" : "Jugador 2")} coronó todas sus fichas en Parchís.";
            return new MoveResult { IsValid = true, IsGameOver = true, Winner = Winner, Message = StatusMessage };
        }

        // Si sacó 6 o comió ficha, repite turno según reglas clásicas de Parchís.
        // En caso contrario, rota turno y lanza el dado para el siguiente jugador.
        if (CurrentDiceValue != 6 && !captured)
        {
            CurrentTurn = CurrentTurn == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
        }

        RollDice();

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
            return new MoveResult { IsValid = false, Message = "Partida finalizada." };

        // Buscar una ficha del turno actual que tenga jugada válida
        foreach (var t in _tokens.Where(tok => tok.Owner == CurrentTurn && !tok.IsInGoal))
        {
            var moves = GetValidMoves(t.Position);
            if (moves.Count > 0)
            {
                return TryMakeMove(t.Position, moves[0]);
            }
        }

        // Si ninguna ficha puede mover, rota turno
        CurrentTurn = CurrentTurn == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
        RollDice();
        return new MoveResult { IsValid = false, Message = "El rival no tuvo jugadas válidas y pasó turno." };
    }

    public void Resign(PlayerSide resigningSide)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        Winner = resigningSide == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
        StatusMessage = $"{(resigningSide == PlayerSide.Player1 ? "Jugador 1" : "Jugador 2")} se retiró del Parchís.";
    }

    public void DeclareDraw()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        IsDraw = true;
        Winner = PlayerSide.None;
        StatusMessage = "Partida de Parchís finalizada en Tablas (Empate acordado).";
    }
}
