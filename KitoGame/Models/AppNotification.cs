using MongoDB.Bson.Serialization.Attributes;

namespace KitoGame.Models;

public enum NotificationType
{
    DirectChallengeReceived,
    PlayerSearchingOpponent,
    PaymentConfirmed,
    MatchSettled
}

[BsonIgnoreExtraElements]
public class AppNotification
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedChallengeId { get; set; }
    public decimal? BetAmount { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string Icon => Type switch
    {
        NotificationType.DirectChallengeReceived => "⚔️",
        NotificationType.PlayerSearchingOpponent => "📢",
        NotificationType.PaymentConfirmed => "✅",
        NotificationType.MatchSettled => "🏆",
        _ => "🔔"
    };

    public string FormattedTime => CreatedAt.ToString("HH:mm");
}

