using KitoGame.Shared.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace KitoGame.AdminWeb.Services;

public class DashboardStats
{
    public long TotalUsers { get; set; }
    public long OnlineUsers { get; set; }
    public long TodayMatches { get; set; }
    public decimal TodayCommissionIncome { get; set; }
    public decimal TotalCirculatingBalance { get; set; }
    public long PendingPaymentsCount { get; set; }
    public long RejectedPaymentsCount { get; set; }
    public long ActiveChallengesCount { get; set; }
    public bool IsDatabaseConnected { get; set; }
    public string DatabaseHost { get; set; } = string.Empty;
}

public class AdminDataService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AdminDataService> _logger;
    private readonly string _localConfigPath;
    private SystemConfig _currentConfig = new();
    private IMongoDatabase? _database;
    private MongoClient? _mongoClient;

    public bool IsConnected { get; private set; }
    public string ConnectionStatusMessage { get; private set; } = "Iniciando servicio de datos...";
    public SystemConfig CurrentConfig => _currentConfig;

    public AdminDataService(IWebHostEnvironment env, ILogger<AdminDataService> logger)
    {
        _env = env;
        _logger = logger;
        _localConfigPath = Path.Combine(_env.ContentRootPath, "admin_db_config.json");

        LoadLocalConfig();
        InitializeDatabaseConnection();
    }

    private void LoadLocalConfig()
    {
        try
        {
            if (File.Exists(_localConfigPath))
            {
                string json = File.ReadAllText(_localConfigPath);
                var loaded = JsonSerializer.Deserialize<SystemConfig>(json);
                if (loaded != null)
                {
                    _currentConfig = loaded;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando configuración local");
        }

        // Configuración por defecto de MongoDB Atlas
        _currentConfig = new SystemConfig();
    }

    private void SaveLocalConfig()
    {
        try
        {
            string json = JsonSerializer.Serialize(_currentConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_localConfigPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error guardando configuración local");
        }
    }

    public void InitializeDatabaseConnection()
    {
        try
        {
            string connectionString = _currentConfig.BuildConnectionString();
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);

            _mongoClient = new MongoClient(settings);
            _database = _mongoClient.GetDatabase(_currentConfig.DatabaseName);

            // Probar conexión rápida
            _database.RunCommand((MongoDB.Driver.Command<BsonDocument>)"{ping:1}");
            IsConnected = true;
            ConnectionStatusMessage = $"🟢 Conectado a {_currentConfig.DatabaseHost} ({_currentConfig.DatabaseName})";
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatusMessage = $"⚠️ Error de conexión: {ex.Message}";
            _logger.LogWarning("Fallo de conexión a MongoDB: {Message}", ex.Message);
        }
    }

    public IMongoCollection<User>? UsersCollection => _database?.GetCollection<User>("users");
    public IMongoCollection<PaymentRequest>? PaymentsCollection => _database?.GetCollection<PaymentRequest>("payment_requests");
    public IMongoCollection<WalletTransaction>? TransactionsCollection => _database?.GetCollection<WalletTransaction>("wallet_transactions");
    public IMongoCollection<MatchChallenge>? ChallengesCollection => _database?.GetCollection<MatchChallenge>("match_challenges");
    public IMongoCollection<SystemConfig>? ConfigCollection => _database?.GetCollection<SystemConfig>("system_config");
    public IMongoCollection<AuditLog>? AuditLogsCollection => _database?.GetCollection<AuditLog>("audit_logs");

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var stats = new DashboardStats
        {
            IsDatabaseConnected = IsConnected,
            DatabaseHost = _currentConfig.DatabaseHost
        };

        if (!IsConnected || _database == null)
            return stats;

        try
        {
            var users = UsersCollection;
            if (users != null)
            {
                stats.TotalUsers = await users.CountDocumentsAsync(FilterDefinition<User>.Empty);
                var allUsers = await users.Find(FilterDefinition<User>.Empty).ToListAsync();
                var fifteenMinsAgo = DateTime.UtcNow.AddMinutes(-15);
                stats.OnlineUsers = allUsers.Count(u => u.LastActiveAt.HasValue && u.LastActiveAt >= fifteenMinsAgo);
                stats.TotalCirculatingBalance = allUsers.Sum(u => u.Balance);
            }

            var payments = PaymentsCollection;
            if (payments != null)
            {
                stats.PendingPaymentsCount = await payments.CountDocumentsAsync(p => p.Status == PaymentStatus.Pending);
                stats.RejectedPaymentsCount = await payments.CountDocumentsAsync(p => p.Status == PaymentStatus.Rejected);
            }

            var challenges = ChallengesCollection;
            if (challenges != null)
            {
                var todayUtc = DateTime.UtcNow.Date;
                var todayChallenges = await challenges.Find(c => c.CreatedAt >= todayUtc).ToListAsync();
                stats.TodayMatches = todayChallenges.Count;
                stats.TodayCommissionIncome = todayChallenges.Sum(c => c.CommissionCharged);
                stats.ActiveChallengesCount = await challenges.CountDocumentsAsync(c => c.Status == MatchStatus.Active || c.Status == MatchStatus.WaitingForOpponent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculando métricas del dashboard");
        }

        return stats;
    }

    public async Task<List<User>> GetUsersAsync(string? search = null, bool? onlyBanned = null)
    {
        if (UsersCollection == null) return new();

        try
        {
            var filterBuilder = Builders<User>.Filter;
            var filter = filterBuilder.Empty;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                var sFilter = filterBuilder.Or(
                    filterBuilder.Regex(u => u.Username, new BsonRegularExpression(s, "i")),
                    filterBuilder.Regex(u => u.FirstName, new BsonRegularExpression(s, "i")),
                    filterBuilder.Regex(u => u.LastName, new BsonRegularExpression(s, "i")),
                    filterBuilder.Regex(u => u.Email, new BsonRegularExpression(s, "i"))
                );
                filter = filterBuilder.And(filter, sFilter);
            }

            if (onlyBanned.HasValue)
            {
                filter = filterBuilder.And(filter, filterBuilder.Eq(u => u.IsBanned, onlyBanned.Value));
            }

            return await UsersCollection.Find(filter).SortByDescending(u => u.CreatedAt).Limit(100).ToListAsync();
        }
        catch
        {
            return new();
        }
    }

    public async Task<User?> GetUserByIdAsync(string id)
    {
        if (UsersCollection == null) return null;
        try
        {
            return await UsersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(string userId, string newPassword, string admin)
    {
        if (UsersCollection == null) return (false, "Sin conexión a la base de datos.");
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
            return (false, "La nueva contraseña debe tener al menos 4 caracteres.");

        try
        {
            var update = Builders<User>.Update.Set(u => u.Password, newPassword);
            var res = await UsersCollection.UpdateOneAsync(u => u.Id == userId, update);

            if (res.ModifiedCount > 0)
            {
                await LogAuditAsync(admin, "ResetPassword", $"User ID: {userId}", $"Contraseña cambiada exitosamente.");
                return (true, "Contraseña actualizada exitosamente.");
            }
            return (false, "No se encontró el usuario o no hubo cambios.");
        }
        catch (Exception ex)
        {
            return (false, $"Error al restablecer contraseña: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> AdjustUserBalanceAsync(string userId, decimal amountDelta, string reason, string admin)
    {
        if (UsersCollection == null) return (false, "Sin conexión a la base de datos.");

        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null) return (false, "Usuario no encontrado.");

            decimal oldBalance = user.Balance;
            decimal newBalance = Math.Max(0, oldBalance + amountDelta);

            var update = Builders<User>.Update.Set(u => u.Balance, newBalance);
            await UsersCollection.UpdateOneAsync(u => u.Id == userId, update);

            // Registrar transacción en billetera
            if (TransactionsCollection != null)
            {
                var tx = new WalletTransaction
                {
                    UserId = user.Id,
                    Username = user.Username,
                    Amount = amountDelta,
                    Type = TransactionType.AdminAdjustment,
                    Description = $"Ajuste administrativo por @{admin}: {reason}"
                };
                await TransactionsCollection.InsertOneAsync(tx);
            }

            await LogAuditAsync(admin, "AdjustBalance", $"User: @{user.Username}", $"Saldo modificado de ${oldBalance:N0} a ${newBalance:N0} COP. Motivo: {reason}");
            return (true, $"Saldo ajustado a ${newBalance:N0} COP.");
        }
        catch (Exception ex)
        {
            return (false, $"Error ajustando saldo: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ToggleUserBanAsync(string userId, string reason, string admin)
    {
        if (UsersCollection == null) return (false, "Sin conexión a la base de datos.");

        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null) return (false, "Usuario no encontrado.");

            bool newBanned = !user.IsBanned;
            var update = Builders<User>.Update
                .Set(u => u.IsBanned, newBanned)
                .Set(u => u.BanReason, newBanned ? reason : null);

            await UsersCollection.UpdateOneAsync(u => u.Id == userId, update);

            string action = newBanned ? "BanUser" : "UnbanUser";
            string desc = newBanned ? $"Usuario suspendido/bloqueado. Motivo: {reason}" : "Usuario reactivado.";
            await LogAuditAsync(admin, action, $"User: @{user.Username}", desc);

            return (true, newBanned ? "Usuario bloqueado del sistema." : "Usuario reactivado.");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DeleteUserAsync(string userId, string admin)
    {
        if (UsersCollection == null) return (false, "Sin conexión a la base de datos.");

        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null) return (false, "Usuario no encontrado.");

            if (user.IsAdmin) return (false, "No se puede eliminar una cuenta de administrador.");

            await UsersCollection.DeleteOneAsync(u => u.Id == userId);
            await LogAuditAsync(admin, "DeleteUser", $"User: @{user.Username}", $"Usuario y registros eliminados permanentemente.");
            return (true, $"Usuario @{user.Username} eliminado.");
        }
        catch (Exception ex)
        {
            return (false, $"Error al eliminar usuario: {ex.Message}");
        }
    }

    public async Task<List<PaymentRequest>> GetPaymentRequestsAsync(PaymentStatus? filterStatus = null)
    {
        if (PaymentsCollection == null) return new();

        try
        {
            var filter = filterStatus.HasValue
                ? Builders<PaymentRequest>.Filter.Eq(p => p.Status, filterStatus.Value)
                : Builders<PaymentRequest>.Filter.Empty;

            return await PaymentsCollection.Find(filter).SortByDescending(p => p.CreatedAt).Limit(100).ToListAsync();
        }
        catch
        {
            return new();
        }
    }

    public async Task<(bool Success, string Message)> ApprovePaymentRequestAsync(string paymentId, string admin)
    {
        if (PaymentsCollection == null || UsersCollection == null)
            return (false, "Sin conexión a la base de datos.");

        try
        {
            var req = await PaymentsCollection.Find(p => p.Id == paymentId).FirstOrDefaultAsync();
            if (req == null) return (false, "Solicitud de pago no encontrada.");
            if (req.Status == PaymentStatus.Confirmed) return (false, "Este pago ya fue confirmado previamente.");

            req.Status = PaymentStatus.Confirmed;
            req.ConfirmedAt = DateTime.UtcNow;
            req.AdminNote = $"Aprobado por @{admin} desde el Backoffice Web";

            await PaymentsCollection.ReplaceOneAsync(p => p.Id == req.Id, req);

            // Abonar saldo al usuario
            var user = await GetUserByIdAsync(req.UserId);
            if (user != null)
            {
                var update = Builders<User>.Update.Inc(u => u.Balance, req.Amount);
                await UsersCollection.UpdateOneAsync(u => u.Id == user.Id, update);

                if (TransactionsCollection != null)
                {
                    var tx = new WalletTransaction
                    {
                        UserId = user.Id,
                        Username = user.Username,
                        Amount = req.Amount,
                        Type = TransactionType.Deposit,
                        Description = $"Recarga aprobada por admin: Ref #{req.ReferenceCode}"
                    };
                    await TransactionsCollection.InsertOneAsync(tx);
                }
            }

            await LogAuditAsync(admin, "ApprovePayment", $"Payment #{req.ReferenceCode}", $"Pago de ${req.Amount:N0} COP aprobado y acreditado a {req.UserFullName}.");
            return (true, $"Pago aprobado y ${req.Amount:N0} COP acreditados al jugador.");
        }
        catch (Exception ex)
        {
            return (false, $"Error aprobando pago: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> RejectPaymentRequestAsync(string paymentId, string reason, string admin)
    {
        if (PaymentsCollection == null) return (false, "Sin conexión a la base de datos.");

        try
        {
            var req = await PaymentsCollection.Find(p => p.Id == paymentId).FirstOrDefaultAsync();
            if (req == null) return (false, "Solicitud de pago no encontrada.");

            req.Status = PaymentStatus.Rejected;
            req.AdminNote = $"Rechazado por @{admin}: {reason}";
            await PaymentsCollection.ReplaceOneAsync(p => p.Id == req.Id, req);

            await LogAuditAsync(admin, "RejectPayment", $"Payment #{req.ReferenceCode}", $"Pago rechazado. Motivo: {reason}");
            return (true, "Pago marcado como rechazado/error.");
        }
        catch (Exception ex)
        {
            return (false, $"Error al rechazar pago: {ex.Message}");
        }
    }

    public async Task<List<WalletTransaction>> GetWalletTransactionsAsync(int take = 50)
    {
        if (TransactionsCollection == null) return new();
        try
        {
            return await TransactionsCollection.Find(FilterDefinition<WalletTransaction>.Empty)
                .SortByDescending(t => t.Timestamp)
                .Limit(take)
                .ToListAsync();
        }
        catch
        {
            return new();
        }
    }

    public async Task<List<AuditLog>> GetAuditLogsAsync(int take = 50)
    {
        if (AuditLogsCollection == null) return new();
        try
        {
            return await AuditLogsCollection.Find(FilterDefinition<AuditLog>.Empty)
                .SortByDescending(a => a.Timestamp)
                .Limit(take)
                .ToListAsync();
        }
        catch
        {
            return new();
        }
    }

    public async Task<(bool Success, string Message)> UpdateDatabaseConfigAsync(SystemConfig newConfig, string admin)
    {
        try
        {
            string testConn = newConfig.BuildConnectionString();
            var testSettings = MongoClientSettings.FromConnectionString(testConn);
            testSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            testSettings.ConnectTimeout = TimeSpan.FromSeconds(5);

            var testClient = new MongoClient(testSettings);
            var testDb = testClient.GetDatabase(newConfig.DatabaseName);
            await testDb.RunCommandAsync((MongoDB.Driver.Command<BsonDocument>)"{ping:1}");

            // Conexión exitosa -> Aplicar cambios
            newConfig.UpdatedAt = DateTime.UtcNow;
            newConfig.UpdatedBy = admin;
            _currentConfig = newConfig;

            SaveLocalConfig();
            InitializeDatabaseConnection();

            // Guardar también en la base de datos para que la app móvil la consulte si lo desea
            if (ConfigCollection != null)
            {
                await ConfigCollection.ReplaceOneAsync(
                    c => c.Id == newConfig.Id,
                    newConfig,
                    new ReplaceOptions { IsUpsert = true });
            }

            await LogAuditAsync(admin, "UpdateDbConfig", "SystemConfig", $"Parámetros de BD actualizados a host: {newConfig.DatabaseHost}");
            return (true, "¡Conexión validada y parámetros de base de datos actualizados correctamente!");
        }
        catch (Exception ex)
        {
            return (false, $"Falló la conexión con los nuevos parámetros: {ex.Message}");
        }
    }

    public async Task LogAuditAsync(string admin, string action, string target, string details)
    {
        try
        {
            if (AuditLogsCollection != null)
            {
                var log = new AuditLog
                {
                    AdminUsername = admin,
                    Action = action,
                    TargetEntity = target,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                };
                await AuditLogsCollection.InsertOneAsync(log);
            }
        }
        catch
        {
        }
    }
}

