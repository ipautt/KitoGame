using MongoDB.Bson.Serialization.Attributes;

namespace KitoGame.Shared.Models;

public enum PaymentStatus
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2
}

[BsonIgnoreExtraElements]
public class PaymentRequest
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ReferenceCode { get; set; } = $"PAY-{Random.Shared.Next(100000, 999999)}";
    public string UserId { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Nequi / PSE";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }

    public string FormattedAmount => $"${Amount:N0} COP";
    public string FormattedDate => CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string StatusText => Status switch
    {
        PaymentStatus.Pending => "⏳ Pendiente de Confirmación",
        PaymentStatus.Confirmed => "✅ Pago Confirmado y Abonado",
        PaymentStatus.Rejected => "❌ Pago Cancelado / Rechazado",
        _ => Status.ToString()
    };
}
