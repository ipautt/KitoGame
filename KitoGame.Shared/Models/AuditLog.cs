using MongoDB.Bson.Serialization.Attributes;

namespace KitoGame.Shared.Models;

[BsonIgnoreExtraElements]
public class AuditLog
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AdminUsername { get; set; } = "ivan";
    public string Action { get; set; } = string.Empty; // Ej: "ResetPassword", "UpdateDbConfig", "AdjustBalance", "BanUser"
    public string TargetEntity { get; set; } = string.Empty; // Ej: "User: Carlos_M" o "SystemConfig"
    public string Details { get; set; } = string.Empty;
    public string IpAddress { get; set; } = "127.0.0.1";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string FormattedDate => Timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
}
