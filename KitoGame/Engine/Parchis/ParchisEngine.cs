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
    public int StepIndex { get; set; } = -1; // -1: En Cárcel/Casa, 0..51: Pista general, 52..56: Pasillo llegada, 57: Meta
    public bool IsInJail => StepIndex == -1;
    public bool IsInGoal => StepIndex >= 57;
    public BoardPosition Position { get; set; }
    public BoardPosition JailHomePosition { get; set; }
}

public class ParchisEngine : IGameEngine
{
    public GameMetadata Metadata { get; } = new()
    {
        Id = "parchis",
        Name = "Parqués / Parchís",
        Icon = "🎲",
        ShortDescription = "El clásico juego tradicional colombiano de dos dados. Saca pares para salir de la cárcel, repite tiro, aplasta fichas rivales y corona el cielo.",
        RulesSummary = "Se juega con 2 dados. Si todas tus fichas están en la cárcel tienes hasta 3 tiros para sacar pares (1-1, 2-2, etc.). Al sacar pares, sales de la cárcel y repites tiro. Mueve sumando los dados. Al comer una ficha rival la mandas a la cárcel y ganas +20 casillas de bonificación. Al coronar en la meta ganas +10. Gana quien corone sus 4 fichas.",
        MinBet = 1000m,
        AccentColorHex = "#F59E0B"
    };

    public int Rows => 15;
    public int Cols => 15;

    public PlayerSide CurrentTurn { get; private set; } = PlayerSide.Player1;
    public bool IsGameOver { get; private set; }
    public PlayerSide Winner { get; private set; } = PlayerSide.None;
    public bool IsDraw { get; private set; }
    public string StatusMessage { get; private set; } = "🎲 ¡Bienvenido al Parqués! Toca '🎲 Tirar' para lanzar los 2 dados.";
    public List<string> MoveHistory { get; } = new();

    public BoardPosition? LastMoveFrom { get; private set; }
    public BoardPosition? LastMoveTo { get; private set; }
    public BoardPosition? ForcedPiecePosition { get; private set; }

    public int Player1PiecesCount => _tokens.Count(t => t.Owner == PlayerSide.Player1 && !t.IsInGoal);
    public int Player2PiecesCount => _tokens.Count(t => t.Owner == PlayerSide.Player2 && !t.IsInGoal);

    // Sistema de 2 Dados oficiales de Parqués
    public int Dice1 { get; private set; } = 1;
    public int Dice2 { get; private set; } = 1;
    public int CurrentDiceValue => TotalDice;
    public int TotalDice => Dice1 + Dice2;
    public bool IsPair => Dice1 == Dice2;
    public int JailRollAttemptsLeft { get; private set; } = 3;

    public bool HasRolledThisTurn { get; private set; } = false;
    public bool CanRollAgain { get; private set; } = false;
    public int PendingBonusSteps { get; private set; } = 0;

    private readonly List<ParchisToken> _tokens = new();
    private readonly Random _random = new();

    // Nidos de las 4 Casas (Cárceles) en el tablero 15x15
    public static readonly BoardPosition[] RedJailSlots = { new(11, 2), new(11, 3), new(12, 2), new(12, 3) };
    public static readonly BoardPosition[] GreenJailSlots = { new(2, 11), new(2, 12), new(3, 11), new(3, 12) };
    public static readonly BoardPosition[] YellowJailSlots = { new(2, 2), new(2, 3), new(3, 2), new(3, 3) };
    public static readonly BoardPosition[] BlueJailSlots = { new(11, 11), new(11, 12), new(12, 11), new(12, 12) };

    // Casilla de Meta Central
    public static readonly BoardPosition CenterGoal = new(7, 7);

    // Circuito de 52 casillas perimetrales (15x15)
    public static readonly BoardPosition[] MainTrack = new BoardPosition[52]
    {
        // 0..4: Brazo inferior izquierdo (sube col 6)
        new(13, 6), new(12, 6), new(11, 6), new(10, 6), new(9, 6),
        // 5..10: Brazo izquierdo inferior (va a la izquierda row 8)
        new(8, 5), new(8, 4), new(8, 3), new(8, 2), new(8, 1), new(8, 0),
        // 11: Extremo izquierdo (Seguro)
        new(7, 0),
        // 12..17: Brazo izquierdo superior (va a la derecha row 6)
        new(6, 0), new(6, 1), new(6, 2), new(6, 3), new(6, 4), new(6, 5),
        // 18..22: Brazo superior izquierdo (sube col 6)
        new(5, 6), new(4, 6), new(3, 6), new(2, 6), new(1, 6),
        // 23..24: Extremo superior
        new(0, 6), new(0, 7),
        // 25..30: Brazo superior derecho (baja col 8)
        new(0, 8), new(1, 8), new(2, 8), new(3, 8), new(4, 8), new(5, 8),
        // 31..36: Brazo derecho superior (va a la derecha row 6)
        new(6, 9), new(6, 10), new(6, 11), new(6, 12), new(6, 13), new(6, 14),
        // 37: Extremo derecho (Seguro)
        new(7, 14),
        // 38..43: Brazo derecho inferior (va a la izquierda row 8)
        new(8, 14), new(8, 13), new(8, 12), new(8, 11), new(8, 10), new(8, 9),
        // 44..48: Brazo inferior derecho (baja col 8)
        new(9, 8), new(10, 8), new(11, 8), new(12, 8), new(13, 8),
        // 49..50: Extremo inferior
        new(14, 8), new(14, 7),
        // 51: Retorno al brazo inferior
        new(14, 6)
    };

    // Pasillos de llegada hacia la meta central (5 casillas exclusivas)
    public static readonly BoardPosition[] RedArrivalLane = { new(13, 7), new(12, 7), new(11, 7), new(10, 7), new(9, 7), new(8, 7) };
    public static readonly BoardPosition[] GreenArrivalLane = { new(1, 7), new(2, 7), new(3, 7), new(4, 7), new(5, 7), new(6, 7) };
    public static readonly BoardPosition[] YellowArrivalLane = { new(7, 1), new(7, 2), new(7, 3), new(7, 4), new(7, 5), new(7, 6) };
    public static readonly BoardPosition[] BlueArrivalLane = { new(7, 13), new(7, 12), new(7, 11), new(7, 10), new(7, 9), new(7, 8) };

    // Índices de Salida en MainTrack
    public const int RedStartIndex = 0;     // (13,6)
    public const int YellowStartIndex = 13; // (6,1)
    public const int GreenStartIndex = 26;  // (1,8)
    public const int BlueStartIndex = 39;   // (8,13)

    // Casillas de Seguro oficiales (12 seguros)
    public static readonly HashSet<int> SafeTrackIndices = new()
    {
        RedStartIndex, YellowStartIndex, GreenStartIndex, BlueStartIndex, // 4 salidas
        11, 24, 37, 50,                                                  // 4 extremos de brazos
        7, 20, 33, 46                                                    // 4 intermedios
    };

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
        HasRolledThisTurn = false;
        CanRollAgain = false;
        PendingBonusSteps = 0;
        Dice1 = 1;
        Dice2 = 1;
        JailRollAttemptsLeft = 3;
        StatusMessage = "🎲 ¡Partida de Parqués iniciada! Toca '🎲 Tirar' para lanzar los 2 dados.";

        // 4 Fichas Jugador 1 (Rojas - Abajo)
        for (int i = 0; i < 4; i++)
        {
            _tokens.Add(new ParchisToken
            {
                Id = i + 1,
                Owner = PlayerSide.Player1,
                Color = ParchisPlayerColor.Red,
                StepIndex = -1,
                JailHomePosition = RedJailSlots[i],
                Position = RedJailSlots[i]
            });
        }

        // 4 Fichas Jugador 2 (Verdes - Arriba / Diagonal opuesta clásica)
        for (int i = 0; i < 4; i++)
        {
            _tokens.Add(new ParchisToken
            {
                Id = i + 5,
                Owner = PlayerSide.Player2,
                Color = ParchisPlayerColor.Green,
                StepIndex = -1,
                JailHomePosition = GreenJailSlots[i],
                Position = GreenJailSlots[i]
            });
        }
    }

    public int RollDice()
    {
        if (IsGameOver) return TotalDice;

        Dice1 = _random.Next(1, 7);
        Dice2 = _random.Next(1, 7);

        string playerTag = CurrentTurn == PlayerSide.Player1 ? "Tú" : "Rival";
        bool allInJail = _tokens.Where(t => t.Owner == CurrentTurn).All(t => t.IsInJail);

        if (allInJail)
        {
            if (IsPair)
            {
                HasRolledThisTurn = true;
                CanRollAgain = true; // Sacar pares repite tiro
                JailRollAttemptsLeft = 3;
                StatusMessage = $"🎉 ¡{playerTag} sacó PARES ({Dice1}-{Dice2})! Toca una ficha de tu casa para salir a la pista. ¡Además repites tiro!";
            }
            else
            {
                JailRollAttemptsLeft--;
                if (JailRollAttemptsLeft > 0)
                {
                    HasRolledThisTurn = false; // Puede seguir tirando en este turno
                    StatusMessage = $"🎲 {playerTag} sacó {Dice1} y {Dice2} (Total: {TotalDice}). Necesitas PARES para salir de la cárcel. Te queda(n) {JailRollAttemptsLeft} intento(s).";
                }
                else
                {
                    HasRolledThisTurn = true;
                    CanRollAgain = false;
                    JailRollAttemptsLeft = 3;
                    StatusMessage = $"🎲 {playerTag} sacó {Dice1} y {Dice2}. Agotaste tus 3 tiros sin sacar pares. Turno cedido.";
                }
            }
        }
        else
        {
            HasRolledThisTurn = true;
            JailRollAttemptsLeft = 3;
            CanRollAgain = IsPair;

            if (IsPair)
            {
                bool hasInJail = _tokens.Any(t => t.Owner == CurrentTurn && t.IsInJail);
                StatusMessage = hasInJail
                    ? $"🎉 ¡{playerTag} sacó PARES ({Dice1}-{Dice2}) = {TotalDice}! Puedes sacar una ficha de la cárcel o mover {TotalDice} casillas. ¡Repites tiro!"
                    : $"🎉 ¡{playerTag} sacó PARES ({Dice1}-{Dice2}) = {TotalDice}! Avanzas {TotalDice} casillas y repites tiro.";
            }
            else
            {
                StatusMessage = $"🎲 {playerTag} sacó {Dice1} y {Dice2} (Total: {TotalDice}). Toca una ficha para mover {TotalDice} casillas.";
            }
        }

        return TotalDice;
    }

    public BoardPiece? GetPieceAt(BoardPosition position)
    {
        var token = _tokens.FirstOrDefault(t => t.Position == position && !t.IsInGoal);
        if (token == null) return null;

        string symbol = token.Color switch
        {
            ParchisPlayerColor.Red => "🔴",
            ParchisPlayerColor.Green => "🟢",
            ParchisPlayerColor.Yellow => "🟡",
            _ => "🔵"
        };

        return new BoardPiece
        {
            Owner = token.Owner,
            Code = $"PARCHIS_{token.Color}_{token.Id}",
            Symbol = symbol,
            IsKingOrPromoted = token.IsInGoal
        };
    }

    public IReadOnlyList<BoardPosition> GetValidMoves(BoardPosition from)
    {
        var moves = new List<BoardPosition>();
        if (IsGameOver) return moves;

        var token = _tokens.FirstOrDefault(t => t.Position == from && t.Owner == CurrentTurn && !t.IsInGoal);
        if (token == null) return moves;

        int stepsToApply = PendingBonusSteps > 0 ? PendingBonusSteps : (HasRolledThisTurn ? TotalDice : 0);
        if (stepsToApply <= 0) return moves;

        // 1. Si la ficha está en la cárcel: solo puede salir si sacó PARES en los dados
        if (token.IsInJail)
        {
            if (PendingBonusSteps == 0 && IsPair)
            {
                var startSquare = GetStartSquare(token.Color);
                // Puede salir si en la casilla de salida no hay ya 2 fichas propias
                int myTokensAtStart = _tokens.Count(t => t.Owner == CurrentTurn && t.Position == startSquare && !t.IsInJail);
                if (myTokensAtStart < 2)
                {
                    moves.Add(startSquare);
                }
            }
            return moves;
        }

        // 2. Si la ficha ya está en pista o pasillo
        var dest = CalculateDestination(token, stepsToApply);
        if (dest.HasValue)
        {
            // Comprobar si hay barrera/puente enemigo en el destino (2 fichas rivales)
            int enemyTokensAtDest = _tokens.Count(t => t.Owner != CurrentTurn && t.Position == dest.Value && !t.IsInGoal);
            if (enemyTokensAtDest < 2)
            {
                moves.Add(dest.Value);
            }
        }

        return moves;
    }

    private BoardPosition GetStartSquare(ParchisPlayerColor color) => color switch
    {
        ParchisPlayerColor.Red => MainTrack[RedStartIndex],
        ParchisPlayerColor.Yellow => MainTrack[YellowStartIndex],
        ParchisPlayerColor.Green => MainTrack[GreenStartIndex],
        _ => MainTrack[BlueStartIndex]
    };

    private BoardPosition? CalculateDestination(ParchisToken token, int steps)
    {
        int totalTrackSteps = 50; // Pasos en pista común antes de entrar al pasillo propio
        int newStep = token.StepIndex + steps;

        // Entrada a la meta central
        if (newStep == 57)
        {
            return CenterGoal;
        }

        // No puede pasarse de la meta con un dado mayor
        if (newStep > 57)
        {
            return null;
        }

        // En pasillo de llegada (steps 51..56)
        if (newStep >= 51)
        {
            int corridorIndex = newStep - 51;
            var corridor = token.Color switch
            {
                ParchisPlayerColor.Red => RedArrivalLane,
                ParchisPlayerColor.Green => GreenArrivalLane,
                ParchisPlayerColor.Yellow => YellowArrivalLane,
                _ => BlueArrivalLane
            };

            if (corridorIndex >= 0 && corridorIndex < corridor.Length)
                return corridor[corridorIndex];

            return CenterGoal;
        }

        // En pista común perimetral (steps 0..50)
        int startOffset = token.Color switch
        {
            ParchisPlayerColor.Red => RedStartIndex,
            ParchisPlayerColor.Yellow => YellowStartIndex,
            ParchisPlayerColor.Green => GreenStartIndex,
            _ => BlueStartIndex
        };

        int trackIndex = (startOffset + newStep) % 52;
        return MainTrack[trackIndex];
    }

    private int FindStepIndexForPosition(ParchisToken token, BoardPosition targetPos)
    {
        if (targetPos == CenterGoal) return 57;

        // Comprobar pasillo de llegada propio
        var corridor = token.Color switch
        {
            ParchisPlayerColor.Red => RedArrivalLane,
            ParchisPlayerColor.Green => GreenArrivalLane,
            ParchisPlayerColor.Yellow => YellowArrivalLane,
            _ => BlueArrivalLane
        };

        for (int i = 0; i < corridor.Length; i++)
        {
            if (corridor[i] == targetPos)
                return 51 + i;
        }

        // Comprobar pista principal perimetral
        int startOffset = token.Color switch
        {
            ParchisPlayerColor.Red => RedStartIndex,
            ParchisPlayerColor.Yellow => YellowStartIndex,
            ParchisPlayerColor.Green => GreenStartIndex,
            _ => BlueStartIndex
        };

        int trackIdx = Array.IndexOf(MainTrack, targetPos);
        if (trackIdx >= 0)
        {
            return (trackIdx - startOffset + 52) % 52;
        }

        return token.StepIndex >= 0 ? token.StepIndex + 1 : 0;
    }

    public MoveResult ApplyDirectMove(BoardPosition from, BoardPosition to)
    {
        if (IsGameOver)
            return new MoveResult { IsValid = false, Message = "La partida ya terminó." };

        // Buscar la ficha en la posición origen
        var token = _tokens.FirstOrDefault(t => t.Position == from && !t.IsInGoal);
        if (token == null)
            return new MoveResult { IsValid = false, Message = "Ficha no encontrada en la posición." };

        CurrentTurn = token.Owner;
        bool captured = false;

        if (token.IsInJail)
        {
            token.StepIndex = 0;
            token.Position = to;

            // Aplastamiento en la casilla de salida
            var enemyOnStart = _tokens.FirstOrDefault(t => t.Position == to && t.Owner != CurrentTurn && !t.IsInGoal && !t.IsInJail);
            if (enemyOnStart != null)
            {
                enemyOnStart.StepIndex = -1;
                enemyOnStart.Position = enemyOnStart.JailHomePosition;
                captured = true;
            }
        }
        else
        {
            token.StepIndex = FindStepIndexForPosition(token, to);
            token.Position = to;

            if (to == CenterGoal || token.StepIndex >= 57)
            {
                token.StepIndex = 57;
                token.Position = CenterGoal;
            }
            else
            {
                int trackIndex = Array.IndexOf(MainTrack, to);
                bool isSafeSquare = trackIndex >= 0 && SafeTrackIndices.Contains(trackIndex);
                if (!isSafeSquare)
                {
                    var enemyToken = _tokens.FirstOrDefault(t => t.Position == to && t.Owner != CurrentTurn && !t.IsInJail && !t.IsInGoal);
                    if (enemyToken != null)
                    {
                        enemyToken.StepIndex = -1;
                        enemyToken.Position = enemyToken.JailHomePosition;
                        captured = true;
                    }
                }
            }
        }

        LastMoveFrom = from;
        LastMoveTo = to;
        HasRolledThisTurn = false;
        CanRollAgain = false;
        PendingBonusSteps = 0;

        // Comprobar victoria
        if (_tokens.Where(t => t.Owner == CurrentTurn).All(t => t.IsInGoal))
        {
            IsGameOver = true;
            Winner = CurrentTurn;
            StatusMessage = $"¡Victoria! {(Winner == PlayerSide.Player1 ? "Jugador 1" : "Jugador 2")} coronó sus 4 fichas en la Meta de Parchís 🏆.";
            return new MoveResult { IsValid = true, IsGameOver = true, Winner = Winner, Message = StatusMessage };
        }

        // Rotar turno al siguiente jugador
        CurrentTurn = CurrentTurn == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
        JailRollAttemptsLeft = 3;

        string nextPlayer = CurrentTurn == PlayerSide.Player1 ? "Tu turno" : "Turno del rival";
        StatusMessage = $"⏳ {nextPlayer} en Parqués. Lanza los dados.";

        return new MoveResult
        {
            IsValid = true,
            CapturedPiece = captured,
            IsGameOver = false,
            Winner = Winner,
            Message = StatusMessage
        };
    }

    public MoveResult TryMakeMove(BoardPosition from, BoardPosition to)
    {
        if (IsGameOver)
            return new MoveResult { IsValid = false, Message = "La partida ya terminó." };

        // Si se llama sin haber tirado dados y sin bonificación (ej: reproduciendo historial de jugadas o sincronización de red),
        // aplicamos el movimiento de forma directa para restaurar la posición con total fidelidad.
        if (!HasRolledThisTurn && PendingBonusSteps == 0)
        {
            return ApplyDirectMove(from, to);
        }

        var token = _tokens.FirstOrDefault(t => t.Position == from && t.Owner == CurrentTurn && !t.IsInGoal);
        if (token == null)
            return new MoveResult { IsValid = false, Message = "Selecciona una de tus fichas." };

        var valids = GetValidMoves(from);
        if (!valids.Contains(to))
            return new MoveResult { IsValid = false, Message = "Movimiento no permitido para el valor del dado." };

        bool captured = false;
        bool reachedGoal = false;
        string actor = CurrentTurn == PlayerSide.Player1 ? "Tú" : "Rival";
        int appliedSteps = PendingBonusSteps > 0 ? PendingBonusSteps : CurrentDiceValue;
        PendingBonusSteps = 0; // Consumir bonificación activa

        // 1. Salida de la cárcel
        if (token.IsInJail)
        {
            token.StepIndex = 0;
            token.Position = to;

            // Si en la salida hay ficha enemiga: ¡Aplastamiento/Captura!
            var enemyOnStart = _tokens.FirstOrDefault(t => t.Position == to && t.Owner != CurrentTurn && !t.IsInGoal && !t.IsInJail);
            if (enemyOnStart != null)
            {
                enemyOnStart.StepIndex = -1;
                enemyOnStart.Position = enemyOnStart.JailHomePosition;
                captured = true;
                PendingBonusSteps = 20;
                MoveHistory.Insert(0, $"⚔️ ¡{actor} sale de casa aplastando una ficha rival! (+20 de bonificación)");
            }
            else
            {
                MoveHistory.Insert(0, $"🎲 {actor} sacó su ficha #{token.Id} de casa a la casilla de salida.");
            }
        }
        else
        {
            token.StepIndex += appliedSteps;
            token.Position = to;

            // 2. Llegada a la meta
            if (to == CenterGoal || token.StepIndex >= 57)
            {
                token.StepIndex = 57;
                token.Position = CenterGoal;
                reachedGoal = true;
                PendingBonusSteps = 10;
                MoveHistory.Insert(0, $"🏆 ¡{actor} coronó su ficha #{token.Id} en la Meta! (+10 de bonificación con otra ficha)");
            }
            else
            {
                // 3. Comprobar captura en casilla normal (no seguro)
                int trackIndex = Array.IndexOf(MainTrack, to);
                bool isSafeSquare = trackIndex >= 0 && SafeTrackIndices.Contains(trackIndex);

                if (!isSafeSquare)
                {
                    var enemyToken = _tokens.FirstOrDefault(t => t.Position == to && t.Owner != CurrentTurn && !t.IsInJail && !t.IsInGoal);
                    if (enemyToken != null)
                    {
                        // Enviar ficha enemiga a su cárcel
                        enemyToken.StepIndex = -1;
                        enemyToken.Position = enemyToken.JailHomePosition;
                        captured = true;
                        PendingBonusSteps = 20;
                        MoveHistory.Insert(0, $"⚔️ ¡Captura! {actor} comió ficha #{enemyToken.Id} rival. ¡Ganas +20 casillas de bonificación!");
                    }
                }

                if (!captured)
                {
                    MoveHistory.Insert(0, $"🎲 {actor} avanzó ficha #{token.Id} {appliedSteps} casillas.");
                }
            }
        }

        LastMoveFrom = from;
        LastMoveTo = to;

        // Comprobar victoria (las 4 fichas en la meta)
        if (_tokens.Where(t => t.Owner == CurrentTurn).All(t => t.IsInGoal))
        {
            IsGameOver = true;
            Winner = CurrentTurn;
            StatusMessage = $"¡Victoria! {(Winner == PlayerSide.Player1 ? "Jugador 1" : "Jugador 2")} coronó sus 4 fichas en la Meta de Parchís 🏆.";
            return new MoveResult { IsValid = true, IsGameOver = true, Winner = Winner, Message = StatusMessage };
        }

        // Si tiene bonificación pendiente por comer (+20) o por meta (+10), comprueba si puede jugarla
        if (PendingBonusSteps > 0)
        {
            bool canApplyBonus = _tokens.Any(t => t.Owner == CurrentTurn && !t.IsInGoal && GetValidMoves(t.Position).Count > 0);
            if (!canApplyBonus)
            {
                PendingBonusSteps = 0;
                CurrentTurn = CurrentTurn == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
                HasRolledThisTurn = false;
                CanRollAgain = false;
                JailRollAttemptsLeft = 3;
                string nxt = CurrentTurn == PlayerSide.Player1 ? "Tu turno" : "Turno del rival";
                StatusMessage = $"Sin movimientos para la bonificación de +20. {nxt} en Parqués.";
                return new MoveResult { IsValid = true, CapturedPiece = captured, Message = StatusMessage };
            }

            HasRolledThisTurn = true;
            StatusMessage = $"🎁 ¡Tienes bonificación de +{PendingBonusSteps} casillas! Toca una de tus fichas para avanzar.";
            return new MoveResult { IsValid = true, CapturedPiece = captured, Message = StatusMessage };
        }

        // Si sacó PARES, repite tiro
        if (CanRollAgain)
        {
            CanRollAgain = false;
            HasRolledThisTurn = false;
            JailRollAttemptsLeft = 3;
            StatusMessage = $"🎉 ¡Sacaste PARES! Tienes un nuevo tiro de dados adicional.";
            return new MoveResult { IsValid = true, CapturedPiece = captured, Message = StatusMessage };
        }

        // De lo contrario, rotar turno al contrincante
        CurrentTurn = CurrentTurn == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
        HasRolledThisTurn = false;
        CanRollAgain = false;
        PendingBonusSteps = 0;
        JailRollAttemptsLeft = 3;

        string nextPlayer = CurrentTurn == PlayerSide.Player1 ? "Tu turno" : "Turno del rival";
        StatusMessage = $"⏳ {nextPlayer} en Parqués. Lanza los dados.";

        return new MoveResult
        {
            IsValid = true,
            CapturedPiece = captured,
            IsGameOver = false,
            Winner = Winner,
            Message = StatusMessage
        };
    }

    public MoveResult TryMakeAutoMove()
    {
        if (IsGameOver)
            return new MoveResult { IsValid = false, Message = "Partida finalizada." };

        // Si no ha lanzado el dado en este turno, lanzarlo
        if (!HasRolledThisTurn && PendingBonusSteps == 0)
        {
            RollDice();
        }

        // Buscar una ficha del turno actual que tenga jugada válida
        var myTokens = _tokens.Where(tok => tok.Owner == CurrentTurn && !tok.IsInGoal).ToList();
        foreach (var t in myTokens)
        {
            var moves = GetValidMoves(t.Position);
            if (moves.Count > 0)
            {
                // Preferir jugada que capture ficha rival o meta gol
                var bestMove = moves.FirstOrDefault(m =>
                {
                    var rival = _tokens.FirstOrDefault(rt => rt.Position == m && rt.Owner != CurrentTurn && !rt.IsInGoal);
                    return rival != null || m == CenterGoal;
                });

                return TryMakeMove(t.Position, bestMove.Equals(default(BoardPosition)) ? moves[0] : bestMove);
            }
        }

        // Si ninguna ficha puede mover, pasa turno
        CurrentTurn = CurrentTurn == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
        HasRolledThisTurn = false;
        CanRollAgain = false;
        PendingBonusSteps = 0;
        JailRollAttemptsLeft = 3;
        StatusMessage = "Sin jugadas posibles para los dados. Turno cedido.";

        return new MoveResult { IsValid = false, Message = StatusMessage };
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

