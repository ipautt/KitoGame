using MongoDB.Bson.Serialization.Attributes;

namespace KitoGame.Shared.Models;

public enum MatchStatus
{
    WaitingForOpponent = 0,
    Active = 1,
    Completed = 2,
    Cancelled = 3,
    Draw = 4
}

[BsonIgnoreExtraElements]
public class MatchChallenge
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    public string GameId { get; set; } = "checkers";
    public string GameName { get; set; } = "Damas Clásicas";
    public string GameIcon { get; set; } = "🔴";

    public string CreatorId { get; set; } = string.Empty;
    public string CreatorUsername { get; set; } = string.Empty;
    public string CreatorDisplayName { get; set; } = string.Empty;

    public string? OpponentId { get; set; }
    public string? OpponentUsername { get; set; }
    public string? OpponentDisplayName { get; set; }

    public string? TargetOpponentUsername { get; set; }

    public decimal BetAmount { get; set; } = 1000m;
    public decimal TotalPot => BetAmount * 2;
    public MatchStatus Status { get; set; } = MatchStatus.WaitingForOpponent;

    public string? WinnerUserId { get; set; }
    public string? WinnerUsername { get; set; }
    public decimal CommissionCharged { get; set; }
    public decimal NetPrizePaid { get; set; }

    public bool IsPracticeBot { get; set; }
    public List<string> SerializedMoves { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    [BsonIgnore]
    public string FormattedBet => $"${BetAmount:N0} COP";
    [BsonIgnore]
    public string FormattedPot => $"${TotalPot:N0} COP";
}

