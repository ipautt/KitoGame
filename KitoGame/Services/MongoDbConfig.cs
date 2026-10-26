using KitoGame.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace KitoGame.Services;

/// <summary>
/// Configuración centralizada de conexión a MongoDB Atlas para KitoGame.
/// Cumple con las normativas de seguridad de MongoDB Atlas sin credenciales quemadas en el repositorio.
/// </summary>
public static class MongoDbConfig
{
    public const string DatabaseName = "KitoGameDb";

    public static string BuildConnectionString() => MongoDbSecretsProvider.GetActiveConnectionString();
}

[BsonIgnoreExtraElements]
public class PlatformSettingsDocument
{
    [BsonId]
    public string Id { get; set; } = "global_settings";
    public decimal CommissionPercentage { get; set; } = 10m;
    public decimal PlatformTreasuryBalance { get; set; } = 0m;
    public List<string> DisabledGameIds { get; set; } = new();
    public bool AutoPaymentsModuleEnabled { get; set; } = false;
    public int WithdrawalHoldHours { get; set; } = 48;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Servicio de persistencia en la nube con MongoDB Atlas.
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
            string conn = MongoDbConfig.BuildConnectionString();
            if (string.IsNullOrWhiteSpace(conn))
            {
                IsConnected = false;
                StatusMessage = "Modo local activo";
                return;
            }

            var settings = MongoClientSettings.FromConnectionString(conn);
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
            StatusMessage = "🟢 Conectado a MongoDB Atlas en la Nube";
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

    public async Task DeleteUserAsync(string userId)
    {
        if (Users == null) return;
        try
        {
            await Users.DeleteOneAsync(u => u.Id == userId);
            IsConnected = true;
        }
        catch
        {
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

    public async Task<bool> PushChallengeMoveAsync(string challengeId, string moveToken)
    {
        if (MatchChallenges == null) return false;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var filter = Builders<MatchChallenge>.Filter.Eq(c => c.Id, challengeId);
                var update = Builders<MatchChallenge>.Update.Push(c => c.SerializedMoves, moveToken);
                var result = await MatchChallenges.UpdateOneAsync(filter, update);
                if (result.IsAcknowledged)
                {
                    IsConnected = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PushChallengeMoveAsync] Intento {attempt} falló: {ex.Message}");
                if (attempt < 3) await Task.Delay(200 * attempt);
            }
        }
        return false;
    }

    public async Task<bool> PushChatMessageAsync(string challengeId, MatchChatMessage message)
    {
        if (MatchChallenges == null) return false;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var filter = Builders<MatchChallenge>.Filter.Eq(c => c.Id, challengeId);
                var update = Builders<MatchChallenge>.Update.Push(c => c.ChatMessages, message);
                var result = await MatchChallenges.UpdateOneAsync(filter, update);
                if (result.IsAcknowledged)
                {
                    IsConnected = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PushChatMessageAsync] Intento {attempt} falló: {ex.Message}");
                if (attempt < 3) await Task.Delay(200 * attempt);
            }
        }
        return false;
    }

    public async Task UpsertChallengeAsync(MatchChallenge challenge)
    {
        if (MatchChallenges == null) return;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await MatchChallenges.ReplaceOneAsync(c => c.Id == challenge.Id, challenge, new ReplaceOptions { IsUpsert = true });
                IsConnected = true;
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpsertChallengeAsync] Intento {attempt} falló: {ex.Message}");
                if (attempt < 3) await Task.Delay(200 * attempt);
            }
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

    public async Task UpsertSettingsAsync(decimal commissionPercentage, decimal treasuryBalance, List<string>? disabledGameIds = null, bool? autoPayments = null, int? withdrawalHoldHours = null)
    {
        if (PlatformSettings == null) return;
        try
        {
            var existing = await PlatformSettings.Find(s => s.Id == "global_settings").FirstOrDefaultAsync();
            var doc = new PlatformSettingsDocument
            {
                Id = "global_settings",
                CommissionPercentage = commissionPercentage,
                PlatformTreasuryBalance = treasuryBalance,
                DisabledGameIds = disabledGameIds ?? existing?.DisabledGameIds ?? new List<string>(),
                AutoPaymentsModuleEnabled = autoPayments ?? existing?.AutoPaymentsModuleEnabled ?? false,
                WithdrawalHoldHours = withdrawalHoldHours ?? existing?.WithdrawalHoldHours ?? 48,
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
