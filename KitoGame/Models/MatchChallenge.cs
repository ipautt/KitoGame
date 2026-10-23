using MongoDB.Bson.Serialization.Attributes;

namespace KitoGame.Models;

public enum MatchStatus
{
    WaitingForOpponent,
    InProgress,
    Completed,
    Cancelled,
    Draw
}

[BsonIgnoreExtraElements]
public class MatchChallenge
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string GameIcon { get; set; } = string.Empty;

    public string CreatorId { get; set; } = string.Empty;
    public string CreatorUsername { get; set; } = string.Empty;

    public string? TargetOpponentId { get; set; }
    public string? TargetOpponentUsername { get; set; }

    public string? OpponentId { get; set; }
    public string? OpponentUsername { get; set; }

    public decimal BetAmount { get; set; }
    public decimal TotalPot => BetAmount * 2;
    public decimal CommissionPercentageSnapshot { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal WinnerPrize { get; set; }

    public MatchStatus Status { get; set; } = MatchStatus.WaitingForOpponent;
    public bool IsPracticeBot { get; set; }
    public List<string> SerializedMoves { get; set; } = new();
    public List<MatchChatMessage> ChatMessages { get; set; } = new();
    public string? WinnerUserId { get; set; }
    public string? WinnerUsername { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }

    public string FormattedBet => $"${BetAmount:N0} COP";
    public string FormattedPot => $"${TotalPot:N0} COP";
    public string TargetDescription => string.IsNullOrWhiteSpace(TargetOpponentUsername)
        ? "Reto Abierto (Cualquier jugador)"
        : $"Reto Directo para @{TargetOpponentUsername}";

    public string StatusBadge => Status switch
    {
        MatchStatus.WaitingForOpponent => "Esperando Rival",
        MatchStatus.InProgress => "En Juego",
        MatchStatus.Completed => $"Ganador: {WinnerUsername}",
        MatchStatus.Draw => "Empate (Reembolsado)",
        MatchStatus.Cancelled => "Cancelado",
        _ => Status.ToString()
    };
}

[BsonIgnoreExtraElements]
public class MatchChatMessage
{
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsSystem { get; set; }
}

