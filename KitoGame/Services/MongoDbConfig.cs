using KitoGame.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace KitoGame.Services;

/// <summary>
/// Configuración centralizada de conexión a MongoDB Atlas para KitoGame.
/// </summary>
public static class MongoDbConfig
{
    public const string DatabaseUser = "ivdapafi_db_user";
    public const string DatabasePassword = "v2D97uyPbkX3QcJa";
    public const string ClusterHost = "kitogamesdb.od2ekmj.mongodb.net";
    public const string DatabaseName = "KitoGameDb";

    public static string BuildConnectionString() =>
        $"mongodb+srv://{DatabaseUser}:{DatabasePassword}@{ClusterHost}/?retryWrites=true&w=majority&appName=KitoGame";
}

[BsonIgnoreExtraElements]
public class PlatformSettingsDocument
{
    [BsonId]
    public string Id { get; set; } = "global_settings";
    public decimal CommissionPercentage { get; set; } = 10m;
    public decimal PlatformTreasuryBalance { get; set; } = 0m;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Servicio de persistencia en la nube con MongoDB Atlas (kitogamesdb.od2ekmj.mongodb.net).
/// </summary>
public class MongoDbService
{
    private IMongoDatabase? _database;
    public bool IsConnected { get; private set; }
    public string StatusMessage { get; private set; } = "Conectando a MongoDB Atlas...";

    public IMongoCollection<User>? Users => _database?.GetCollection<User>("users");
    public IMongoCollection<PaymentRequest>? PaymentRequests => _database?.GetCollection<PaymentRequest>("payment_requests");
    public IMongoCollection<WalletTransaction>? WalletTransactions => _database?.GetCollection<WalletTransaction>("wallet_transactions");
    public IMongoCollection<MatchChallenge>? MatchChallenges => _database?.GetCollection<MatchChallenge>("match_challenges");
    public IMongoCollection<AppNotification>? Notifications => _database?.GetCollection<AppNotification>("notifications");
    public IMongoCollection<PlatformSettingsDocument>? PlatformSettings => _database?.GetCollection<PlatformSettingsDocument>("platform_settings");

    public MongoDbService()
    {
        try
        {
            var settings = MongoClientSettings.FromConnectionString(MongoDbConfig.BuildConnectionString());
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(6);
            settings.ConnectTimeout = TimeSpan.FromSeconds(6);
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);

            var client = new MongoClient(settings);
            _database = client.GetDatabase(MongoDbConfig.DatabaseName);
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusMessage = $"Modo local activo ({ex.GetType().Name})";
        }
    }

    public async Task<bool> PingAsync()
    {
        if (_database == null)
            return false;

        try
        {
            await _database.RunCommandAsync((MongoDB.Driver.Command<BsonDocument>)"{ping:1}");
            IsConnected = true;
            StatusMessage = $"🟢 Conectado a MongoDB Atlas ({MongoDbConfig.ClusterHost})";
            return true;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusMessage = $"⚠️ Sin conexión a Atlas ({ex.Message})";
            return false;
        }
    }

    public async Task UpsertUserAsync(User user)
    {
        if (Users == null) return;
        try
        {
            await Users.ReplaceOneAsync(u => u.Id == user.Id, user, new ReplaceOptions { IsUpsert = true });
            IsConnected = true;
        }
        catch
        {
            // Respaldo local continúa operando sin interrumpir al usuario
        }
    }

    public async Task<MatchChallenge?> GetChallengeByIdAsync(string challengeId)
    {
        if (MatchChallenges == null) return null;
        try
        {
            return await MatchChallenges.Find(c => c.Id == challengeId).FirstOrDefaultAsync();
        }
        catch
        {
            return null;
        }
    }

    public async Task DeleteLegacySimulatedChallengesAsync()
    {
        if (MatchChallenges == null) return;
        try
        {
            await MatchChallenges.DeleteManyAsync(c => c.CreatorId == "RIVAL-ONLINE" && !c.IsPracticeBot);
        }
        catch
        {
        }
    }

    public async Task UpsertChallengeAsync(MatchChallenge challenge)
    {
        if (MatchChallenges == null) return;
        try
        {
            await MatchChallenges.ReplaceOneAsync(c => c.Id == challenge.Id, challenge, new ReplaceOptions { IsUpsert = true });
            IsConnected = true;
        }
        catch
        {
        }
    }

    public async Task UpsertPaymentRequestAsync(PaymentRequest request)
    {
        if (PaymentRequests == null) return;
        try
        {
            await PaymentRequests.ReplaceOneAsync(p => p.Id == request.Id, request, new ReplaceOptions { IsUpsert = true });
            IsConnected = true;
        }
        catch
        {
        }
    }

    public async Task UpsertTransactionAsync(WalletTransaction tx)
    {
        if (WalletTransactions == null) return;
        try
        {
            await WalletTransactions.ReplaceOneAsync(t => t.Id == tx.Id, tx, new ReplaceOptions { IsUpsert = true });
            IsConnected = true;
        }
        catch
        {
        }
    }

    public async Task UpsertNotificationAsync(AppNotification notif)
    {
        if (Notifications == null) return;
        try
        {
            await Notifications.ReplaceOneAsync(n => n.Id == notif.Id, notif, new ReplaceOptions { IsUpsert = true });
            IsConnected = true;
        }
        catch
        {
        }
    }

    public async Task UpsertSettingsAsync(decimal commissionPercentage, decimal treasuryBalance)
    {
        if (PlatformSettings == null) return;
        try
        {
            var doc = new PlatformSettingsDocument
            {
                Id = "global_settings",
                CommissionPercentage = commissionPercentage,
                PlatformTreasuryBalance = treasuryBalance,
                UpdatedAt = DateTime.UtcNow
            };
            await PlatformSettings.ReplaceOneAsync(s => s.Id == "global_settings", doc, new ReplaceOptions { IsUpsert = true });
            IsConnected = true;
        }
        catch
        {
        }
    }
}
