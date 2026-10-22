using MongoDB.Bson.Serialization.Attributes;

namespace KitoGame.Shared.Models;

[BsonIgnoreExtraElements]
public class SystemConfig
{
    [BsonId]
    public string Id { get; set; } = "system_master_config";

    // Parámetros de la Base de Datos
    public string DatabaseHost { get; set; } = "kitogamesdb.od2ekmj.mongodb.net";
    public string DatabaseUser { get; set; } = "ivdapafi_db_user";
    public string DatabasePassword { get; set; } = "v2D97uyPbkX3QcJa";
    public string DatabaseName { get; set; } = "KitoGameDb";
    public bool IsSrv { get; set; } = true;
    public string CustomConnectionString { get; set; } = string.Empty;

    // Reglas Globales de KitoGame
    public decimal CommissionPercentage { get; set; } = 10m;
    public decimal WelcomeBonusAmount { get; set; } = 5000m;
    public decimal MinBetAmount { get; set; } = 1000m;
    public decimal MaxBetAmount { get; set; } = 500000m;

    // Control de Estado del Sistema
    public bool MaintenanceMode { get; set; } = false;
    public string MaintenanceMessage { get; set; } = "KitoGame se encuentra en mantenimiento programado. Volvemos en breve.";

    // Auditoría
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "ivan";

    public string BuildConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(CustomConnectionString))
            return CustomConnectionString.Trim();

        if (IsSrv)
            return $"mongodb+srv://{DatabaseUser}:{DatabasePassword}@{DatabaseHost}/?retryWrites=true&w=majority&appName=KitoGame";

        return $"mongodb://{DatabaseUser}:{DatabasePassword}@{DatabaseHost}/{DatabaseName}?retryWrites=true&w=majority";
    }
}
