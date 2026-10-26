using MongoDB.Bson.Serialization.Attributes;

namespace KitoGame.Shared.Models;

[BsonIgnoreExtraElements]
public class SystemConfig
{
    [BsonId]
    public string Id { get; set; } = "system_master_config";

    // Parámetros de la Base de Datos (Configurados de forma segura en local o variables de entorno)
    public string DatabaseHost { get; set; } = string.Empty;
    public string DatabaseUser { get; set; } = string.Empty;
    public string DatabasePassword { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "KitoGameDb";
    public bool IsSrv { get; set; } = false;
    public string CustomConnectionString { get; set; } = string.Empty;

    // Reglas Globales de KitoGame
    public decimal CommissionPercentage { get; set; } = 10m;
    public decimal WelcomeBonusAmount { get; set; } = 5000m;
    public decimal MinBetAmount { get; set; } = 1000m;
    public decimal MaxBetAmount { get; set; } = 500000m;

    // Control de Estado del Sistema
    public bool MaintenanceMode { get; set; } = false;
    public string MaintenanceMessage { get; set; } = "KitoGame se encuentra en mantenimiento programado. Volvemos en breve.";

    // Catálogo de Juegos Habilitados/Deshabilitados
    public List<string> DisabledGameIds { get; set; } = new();

    // Módulo de Pagos Automáticos y Retiros (Nequi / Daviplata)
    public bool AutoPaymentsModuleEnabled { get; set; } = false;
    public int WithdrawalHoldHours { get; set; } = 48;

    // Auditoría
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "admin";

    public string BuildConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(CustomConnectionString))
            return CustomConnectionString.Trim();

        // 1. Variable de entorno
        string? envConn = Environment.GetEnvironmentVariable("KITOGAME_MONGODB_URI")
                       ?? Environment.GetEnvironmentVariable("MONGODB_URI");
        if (!string.IsNullOrWhiteSpace(envConn))
            return envConn.Trim();

        if (string.IsNullOrWhiteSpace(DatabaseHost) || string.IsNullOrWhiteSpace(DatabaseUser) || string.IsNullOrWhiteSpace(DatabasePassword))
            return string.Empty;

        string user = Uri.EscapeDataString(DatabaseUser.Trim());
        string pass = Uri.EscapeDataString(DatabasePassword.Trim());
        string host = DatabaseHost.Trim();
        string db = string.IsNullOrWhiteSpace(DatabaseName) ? "KitoGameDb" : DatabaseName.Trim();

        // Si el host es de MongoDB Atlas (*.mongodb.net)
        if (host.EndsWith(".mongodb.net", StringComparison.OrdinalIgnoreCase))
        {
            // Shard individual o réplica completa
            if (host.Contains("shard-00", StringComparison.OrdinalIgnoreCase))
            {
                return $"mongodb://{user}:{pass}@{host}/{db}?ssl=true&authSource=admin&replicaSet=atlas-71p6ro-shard-0&retryWrites=true&w=majority&appName=KitoGame";
            }

            // Clúster kitogamesdb en modo réplica directa (para redes celulares y WiFi sin SRV)
            if (!IsSrv && host.Contains("kitogamesdb", StringComparison.OrdinalIgnoreCase))
            {
                return $"mongodb://{user}:{pass}@ac-chffaye-shard-00-00.od2ekmj.mongodb.net:27017,ac-chffaye-shard-00-01.od2ekmj.mongodb.net:27017,ac-chffaye-shard-00-02.od2ekmj.mongodb.net:27017/{db}?ssl=true&authSource=admin&replicaSet=atlas-71p6ro-shard-0&retryWrites=true&w=majority&appName=KitoGame";
            }

            // Modo SRV estándar de Atlas
            return $"mongodb+srv://{user}:{pass}@{host}/?authSource=admin&retryWrites=true&w=majority&appName=KitoGame";
        }

        if (IsSrv)
            return $"mongodb+srv://{user}:{pass}@{host}/?authSource=admin&retryWrites=true&w=majority&appName=KitoGame";

        return $"mongodb://{user}:{pass}@{host}/{db}?authSource=admin&retryWrites=true&w=majority";
    }
}

