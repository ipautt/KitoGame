using MongoDB.Bson.Serialization.Attributes;

namespace KitoGame.Models;

public enum TransactionType
{
    Deposit,
    Withdrawal,
    BetLocked,
    BetRefund,
    PrizeWon,
    PlatformCommission
}

[BsonIgnoreExtraElements]
public class WalletTransaction
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [BsonIgnore]
    public string FormattedAmount => Amount >= 0 ? $"+${Amount:N0} COP" : $"-${Math.Abs(Amount):N0} COP";
    [BsonIgnore]
    public string FormattedDate => Timestamp.ToString("dd/MM/yyyy HH:mm");
    [BsonIgnore]
    public Color AmountColor => Amount >= 0 ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626");
}

