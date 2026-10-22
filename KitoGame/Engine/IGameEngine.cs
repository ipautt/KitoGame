using KitoGame.Models;

namespace KitoGame.Engine;

public enum PlayerSide
{
    None = 0,
    Player1 = 1, // Usuario local (Abajo hacia arriba - Fichas Rojas / Blancas)
    Player2 = 2  // Contrincante (Arriba hacia abajo - Fichas Negras / Doradas)
}

public readonly record struct BoardPosition(int Row, int Col)
{
    public bool IsInsideBoard(int rows = 8, int cols = 8) =>
        Row >= 0 && Row < rows && Col >= 0 && Col < cols;
}

public class BoardPiece
{
    public PlayerSide Owner { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public bool IsKingOrPromoted { get; set; }
}

public class MoveResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool CapturedPiece { get; set; }
    public bool HasChainJump { get; set; }
    public BoardPosition? ChainJumpFrom { get; set; }
    public bool IsGameOver { get; set; }
    public PlayerSide Winner { get; set; } = PlayerSide.None;
    public bool IsDraw { get; set; }
}

/// <summary>
/// Contrato universal para cualquier juego 1v1 en KitoGame (Damas, Ajedrez, Parqués, Triqui, etc.).
/// </summary>
public interface IGameEngine
{
    GameMetadata Metadata { get; }
    int Rows { get; }
    int Cols { get; }
    PlayerSide CurrentTurn { get; }
    bool IsGameOver { get; }
    PlayerSide Winner { get; }
    bool IsDraw { get; }
    string StatusMessage { get; }
    List<string> MoveHistory { get; }

    BoardPosition? LastMoveFrom { get; }
    BoardPosition? LastMoveTo { get; }
    BoardPosition?ForcedPiecePosition { get; }
    int Player1PiecesCount { get; }
    int Player2PiecesCount { get; }

    void ResetBoard();
    BoardPiece? GetPieceAt(BoardPosition position);
    IReadOnlyList<BoardPosition> GetValidMoves(BoardPosition from);
    MoveResult TryMakeMove(BoardPosition from, BoardPosition to);
    MoveResult TryMakeAutoMove();
    void Resign(PlayerSide resigningSide);
    void DeclareDraw();
}

