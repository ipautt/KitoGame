using System.Text.Json;
using KitoGame.Engine;
using KitoGame.Models;
using MongoDB.Driver;

namespace KitoGame.Services;

/// <summary>
/// Estructura de la Base de Datos Temporal local (Tablas relacionales persistidas en el dispositivo).
/// </summary>
public class TemporaryDatabaseSchema
{
    public string LanguageCode { get; set; } = "es";
    public decimal CommissionPercentage { get; set; } = 10m;
    public decimal PlatformTreasuryBalance { get; set; } = 0m;
    public string? LastLoggedInUserId { get; set; }

    public List<User> UsersTable { get; set; } = new();
    public List<PaymentRequest> PaymentRequestsTable { get; set; } = new();
    public List<WalletTransaction> WalletTransactionsTable { get; set; } = new();
    public List<MatchChallenge> MatchChallengesTable { get; set; } = new();
    public List<AppNotification> NotificationsTable { get; set; } = new();
}

public class AppStateService
{
    private readonly string _dbFilePath;
    private TemporaryDatabaseSchema _db = new();

    public GameRegistry GameRegistry { get; }
    public LocalizationService Localization { get; }
    public MongoDbService MongoDb { get; } = new();
    public User? CurrentUser { get; private set; }

    public event Action? StateChanged;
    public event Action<AppNotification>? NotificationTriggered;

    public AppStateService(GameRegistry gameRegistry, LocalizationService localization)
    {
        GameRegistry = gameRegistry;
        Localization = localization;
        _dbFilePath = Path.Combine(FileSystem.AppDataDirectory, "kitogame_temp_db_v3.json");
        LoadOrInitializeDatabase();
        Localization.SetLanguage(_db.LanguageCode);
        _ = SyncWithMongoAtlasAsync();
    }

    public decimal CommissionPercentage => _db.CommissionPercentage;
    public decimal PlatformTreasuryBalance => _db.PlatformTreasuryBalance;
    public IReadOnlyList<User> Users => _db.UsersTable;
    public IReadOnlyList<MatchChallenge> Challenges => _db.MatchChallengesTable.OrderByDescending(c => c.CreatedAt).ToList();

    public void ChangeLanguage(string langCode)
    {
        _db.LanguageCode = langCode;
        Localization.SetLanguage(langCode);
        SaveDatabase();
        NotifyStateChanged();
    }

    public IReadOnlyList<WalletTransaction> GetCurrentUserTransactions()
    {
        if (CurrentUser == null) return Array.Empty<WalletTransaction>();
        return _db.WalletTransactionsTable
            .Where(t => t.UserId == CurrentUser.Id)
            .OrderByDescending(t => t.Timestamp)
            .ToList();
    }

    public IReadOnlyList<PaymentRequest> GetCurrentUserPaymentRequests()
    {
        if (CurrentUser == null) return Array.Empty<PaymentRequest>();
        return _db.PaymentRequestsTable
            .Where(p => p.UserId == CurrentUser.Id)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<AppNotification> GetCurrentUserNotifications()
    {
        if (CurrentUser == null) return Array.Empty<AppNotification>();
        return _db.NotificationsTable
            .Where(n => n.UserId == CurrentUser.Id)
            .OrderByDescending(n => n.CreatedAt)
            .ToList();
    }

    public int GetUnreadNotificationsCount()
    {
        if (CurrentUser == null) return 0;
        return _db.NotificationsTable.Count(n => n.UserId == CurrentUser.Id && !n.IsRead);
    }

    public void MarkAllNotificationsAsRead()
    {
        if (CurrentUser == null) return;
        foreach (var n in _db.NotificationsTable.Where(n => n.UserId == CurrentUser.Id))
        {
            n.IsRead = true;
        }
        SaveDatabase();
        NotifyStateChanged();
    }

    public async Task<(bool Success, string Message)> RegisterUserAsync(
        string firstName,
        string lastName,
        string email,
        string phone,
        string username,
        string password)
    {
        firstName = firstName.Trim();
        lastName = lastName.Trim();
        email = email.Trim();
        phone = phone.Trim();
        username = username.Trim();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return (false, "Por favor ingresa tus nombres y apellidos completos.");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return (false, "Ingresa un correo electrónico válido.");

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, "Ingresa un nombre de usuario y contraseña.");

        if (_db.UsersTable.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            return (false, "Ese nombre de usuario ya está registrado.");

        if (_db.UsersTable.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            return (false, "Ese correo electrónico ya está registrado.");

        bool isMasterAdmin = IsMasterAdminAccount(username, email);

        var newUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = phone,
            Username = username,
            Password = password,
            AuthProvider = "Local",
            Balance = 5000m,
            LockedBalance = 0m,
            IsAdmin = isMasterAdmin
        };

        _db.UsersTable.Add(newUser);
        CurrentUser = newUser;
        _db.LastLoggedInUserId = newUser.Id;

        AddTransaction(newUser, 5000m, TransactionType.Deposit, "Saldo inicial de bienvenida ($5.000 COP)");
        SaveDatabase();
        
        bool cloudSaved = false;
        try
        {
            await MongoDb.UpsertUserAsync(newUser);
            cloudSaved = MongoDb.IsConnected;
        }
        catch
        {
            cloudSaved = false;
        }
        NotifyStateChanged();

        if (cloudSaved)
        {
            return (true, $"¡Cuenta creada y sincronizada con la nube para {newUser.FullName} con $5.000 COP de saldo inicial!");
        }
        else
        {
            return (true, $"¡Cuenta creada para {newUser.FullName}! (Nota: Se sincronizará con Atlas en cuanto el celular conecte al cluster).");
        }
    }

    /// <summary>
    /// Inicio de sesión o registro automático mediante cuenta de Google (@gmail.com).
    /// </summary>
    public (bool Success, string Message) LoginWithGoogle(string googleEmail, string fullName)
    {
        googleEmail = googleEmail.Trim().ToLowerInvariant();
        fullName = fullName.Trim();

        if (string.IsNullOrWhiteSpace(googleEmail) || !googleEmail.Contains('@'))
            return (false, "Ingresa un correo de Google válido (ej: usuario@gmail.com).");

        var existingUser = _db.UsersTable.FirstOrDefault(u =>
            u.Email.Equals(googleEmail, StringComparison.OrdinalIgnoreCase));

        if (existingUser != null)
        {
            existingUser.AuthProvider = "Google";
            existingUser.IsAdmin = IsMasterAdminAccount(existingUser.Username, existingUser.Email);
            CurrentUser = existingUser;
            _db.LastLoggedInUserId = existingUser.Id;
            EnsureSampleIncomingChallengesForUser(existingUser);
            SaveDatabase();
            _ = MongoDb.UpsertUserAsync(existingUser);
            NotifyStateChanged();
            return (true, $"Sesión iniciada con Google como {existingUser.DisplayName} ({existingUser.Email}).");
        }

        // Separar nombre y apellido si es cuenta nueva de Google
        string[] parts = string.IsNullOrWhiteSpace(fullName)
            ? new[] { googleEmail.Split('@')[0], "Google" }
            : fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        string firstName = parts.Length > 0 ? parts[0] : "Usuario";
        string lastName = parts.Length > 1 ? parts[1] : "Google";
        string baseUsername = googleEmail.Split('@')[0].Replace(".", "_");

        var newGoogleUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = googleEmail,
            Username = baseUsername,
            Password = Guid.NewGuid().ToString("N")[..8],
            AuthProvider = "Google",
            Balance = 5000m,
            LockedBalance = 0m,
            IsAdmin = IsMasterAdminAccount(baseUsername, googleEmail)
        };

        _db.UsersTable.Add(newGoogleUser);
        CurrentUser = newGoogleUser;
        _db.LastLoggedInUserId = newGoogleUser.Id;

        AddTransaction(newGoogleUser, 5000m, TransactionType.Deposit, "Bono de bienvenida (Cuenta vinculada con Google)");
        EnsureSampleIncomingChallengesForUser(newGoogleUser);
        SaveDatabase();
        _ = MongoDb.UpsertUserAsync(newGoogleUser);
        NotifyStateChanged();

        return (true, $"¡Cuenta de Google vinculada con éxito para {newGoogleUser.FullName} ({newGoogleUser.Email})!");
    }

    public (bool Success, string Message) Login(string usernameOrEmail, string password)
    {
        usernameOrEmail = usernameOrEmail.Trim();
        var user = _db.UsersTable.FirstOrDefault(u =>
            (u.Username.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase) ||
             u.Email.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase)) &&
            u.Password == password);

        if (user == null)
            return (false, "Credenciales incorrectas. Verifica tu usuario/correo y contraseña.");

        if (user.IsBanned)
        {
            string reason = !string.IsNullOrWhiteSpace(user.BanReason) ? $" Motivo: {user.BanReason}" : "";
            return (false, $"🚫 Tu cuenta ha sido suspendida/restringida por el administrador.{reason}");
        }

        CurrentUser = user;
        _db.LastLoggedInUserId = user.Id;
        EnsureSampleIncomingChallengesForUser(user);
        SaveDatabase();
        NotifyStateChanged();
        return (true, $"Bienvenido de nuevo, {user.FullName}");
    }

    public (bool Success, string Message) UpdateUserProfile(
        string firstName,
        string lastName,
        string email,
        string phone,
        string? newPassword,
        bool notificationsEnabled = true)
    {
        if (CurrentUser == null)
            return (false, "No hay una sesión activa.");

        firstName = firstName.Trim();
        lastName = lastName.Trim();
        email = email.Trim();
        phone = phone.Trim();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return (false, "El nombre y apellido no pueden estar vacíos.");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return (false, "Ingresa un correo electrónico válido.");

        if (_db.UsersTable.Any(u => u.Id != CurrentUser.Id && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            return (false, "Ese correo electrónico ya pertenece a otro usuario.");

        CurrentUser.FirstName = firstName;
        CurrentUser.LastName = lastName;
        CurrentUser.Email = email;
        CurrentUser.Phone = phone;
        CurrentUser.NotificationsEnabled = notificationsEnabled;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            CurrentUser.Password = newPassword.Trim();
        }

        SaveDatabase();
        NotifyStateChanged();
        return (true, "Tus datos de perfil se actualizaron correctamente.");
    }

    /// <summary>
    /// Inscribe o actualiza la cuenta bancaria o billetera digital a donde irá el dinero retirado.
    /// </summary>
    public (bool Success, string Message) SavePayoutAccount(
        string bankOrWallet,
        string accountType,
        string accountNumber,
        string documentId)
    {
        if (CurrentUser == null)
            return (false, "No has iniciado sesión.");

        bankOrWallet = bankOrWallet.Trim();
        accountType = accountType.Trim();
        accountNumber = accountNumber.Trim();
        documentId = documentId.Trim();

        if (string.IsNullOrWhiteSpace(bankOrWallet))
            return (false, "Selecciona la entidad o billetera (ej: Nequi, Daviplata, Bancolombia).");

        if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Length < 6)
            return (false, "Ingresa un número de cuenta o celular válido.");

        if (string.IsNullOrWhiteSpace(documentId) || documentId.Length < 5)
            return (false, "Ingresa el número de documento/cédula del titular de la cuenta.");

        CurrentUser.PayoutBankOrWallet = bankOrWallet;
        CurrentUser.PayoutAccountType = string.IsNullOrWhiteSpace(accountType) ? "Ahorros / Billetera" : accountType;
        CurrentUser.PayoutAccountNumber = accountNumber;
        CurrentUser.PayoutDocumentId = documentId;

        SaveDatabase();
        NotifyStateChanged();
        return (true, $"Cuenta inscrita correctamente: {CurrentUser.PayoutAccountSummary}. Todos tus retiros se enviarán a esta cuenta.");
    }

    public void Logout()
    {
        CurrentUser = null;
        _db.LastLoggedInUserId = null;
        SaveDatabase();
        NotifyStateChanged();
    }

    public (bool Success, string Message, PaymentRequest? Request) CreatePaymentRequest(decimal amount, string paymentMethod)
    {
        if (CurrentUser == null)
            return (false, "Inicia sesión primero.", null);

        if (amount < 1000m)
            return (false, "El monto mínimo de recarga es de $1.000 COP.", null);

        var req = new PaymentRequest
        {
            UserId = CurrentUser.Id,
            UserFullName = CurrentUser.FullName,
            Amount = amount,
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Nequi / PSE" : paymentMethod,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.Now
        };

        _db.PaymentRequestsTable.Add(req);
        SaveDatabase();
        NotifyStateChanged();

        return (true, $"Solicitud {req.ReferenceCode} por ${amount:N0} COP creada en estado PENDIENTE. El saldo se abonará únicamente cuando confirmes el pago.", req);
    }

    public (bool Success, string Message) ConfirmPaymentRequest(string paymentRequestId)
    {
        if (CurrentUser == null)
            return (false, "Inicia sesión primero.");

        var req = _db.PaymentRequestsTable.FirstOrDefault(p => p.Id == paymentRequestId);
        if (req == null)
            return (false, "No se encontró la solicitud de pago.");

        if (req.Status != PaymentStatus.Pending)
            return (false, "Esta solicitud de pago ya fue procesada anteriormente.");

        var user = _db.UsersTable.FirstOrDefault(u => u.Id == req.UserId);
        if (user == null)
            return (false, "Usuario de la transacción no encontrado.");

        req.Status = PaymentStatus.Confirmed;
        req.ConfirmedAt = DateTime.Now;
        user.Balance += req.Amount;

        AddTransaction(
            user,
            req.Amount,
            TransactionType.Deposit,
            $"Recarga aprobada ({req.PaymentMethod} • Ref: {req.ReferenceCode})");

        AddNotification(
            user.Id,
            NotificationType.PaymentConfirmed,
            "Pago Confirmado y Abonado",
            $"Tu recarga {req.ReferenceCode} por ${req.Amount:N0} COP fue aprobada.",
            null,
            req.Amount);

        SaveDatabase();
        NotifyStateChanged();
        return (true, $"¡Pago {req.ReferenceCode} confirmado! Se abonaron ${req.Amount:N0} COP a tu saldo disponible.");
    }

    public (bool Success, string Message) RejectPaymentRequest(string paymentRequestId)
    {
        var req = _db.PaymentRequestsTable.FirstOrDefault(p => p.Id == paymentRequestId);
        if (req == null || req.Status != PaymentStatus.Pending)
            return (false, "La solicitud no está pendiente.");

        req.Status = PaymentStatus.Rejected;
        SaveDatabase();
        NotifyStateChanged();
        return (true, $"Solicitud {req.ReferenceCode} cancelada.");
    }

    public (bool Success, string Message) WithdrawBalance(decimal amount)
    {
        if (CurrentUser == null)
            return (false, "No has iniciado sesión.");

        if (!CurrentUser.HasRegisteredPayoutAccount)
        {
            return (false, "⚠️ No tienes una cuenta inscrita para retiros. Primero inscribe tu cuenta (Nequi, Daviplata o Banco) en la tarjeta 'Cuenta Destino para Retiros' aquí mismo.");
        }

        if (amount <= 0)
            return (false, "El monto a retirar debe ser mayor a $0.");

        if (CurrentUser.Balance < amount)
            return (false, $"Saldo disponible insuficiente (${CurrentUser.Balance:N0} COP).");

        CurrentUser.Balance -= amount;
        string destinationInfo = $"{CurrentUser.PayoutBankOrWallet} ({CurrentUser.PayoutAccountType}) Nro: {CurrentUser.PayoutAccountNumber} [Titular: {CurrentUser.DisplayName} • Doc: {CurrentUser.PayoutDocumentId}]";

        AddTransaction(
            CurrentUser,
            -amount,
            TransactionType.Withdrawal,
            $"Retiro enviado a {destinationInfo}");

        SaveDatabase();
        NotifyStateChanged();
        return (true, $"✅ Retiro exitoso por ${amount:N0} COP.\n\nCuenta destino:\n{destinationInfo}");
    }

    public (bool Success, string Message) UpdateCommissionPercentage(decimal newPercentage)
    {
        if (newPercentage < 0m || newPercentage > 50m)
            return (false, "El porcentaje de comisión debe estar entre 0% y 50%.");

        _db.CommissionPercentage = Math.Round(newPercentage, 1);
        SaveDatabase();
        NotifyStateChanged();
        return (true, $"Comisión del sistema actualizada al {_db.CommissionPercentage:0.#}%.");
    }

    public (bool Success, string Message) ResetUserPassword(string userId, string newPassword)
    {
        if (CurrentUser?.IsAdmin != true)
            return (false, "Acción permitida solo para el administrador.");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
            return (false, "La nueva contraseña debe tener al menos 4 caracteres.");

        var user = _db.UsersTable.FirstOrDefault(u => u.Id == userId);
        if (user == null)
            return (false, "Usuario no encontrado.");

        user.Password = newPassword;
        SaveDatabase();
        NotifyStateChanged();
        _ = MongoDb.UpsertUserAsync(user);

        return (true, $"Contraseña actualizada para @{user.Username}.");
    }

    public (bool Success, string Message) ToggleBanUser(string userId, string reason)
    {
        if (CurrentUser?.IsAdmin != true)
            return (false, "Acción permitida solo para el administrador.");

        var user = _db.UsersTable.FirstOrDefault(u => u.Id == userId);
        if (user == null)
            return (false, "Usuario no encontrado.");

        if (user.IsAdmin)
            return (false, "No se puede suspender una cuenta de administrador.");

        user.IsBanned = !user.IsBanned;
        user.BanReason = user.IsBanned ? reason : null;
        SaveDatabase();
        NotifyStateChanged();
        _ = MongoDb.UpsertUserAsync(user);

        return (true, user.IsBanned ? $"🚫 Usuario @{user.Username} suspendido." : $"✅ Usuario @{user.Username} reactivado.");
    }

    public (bool Success, string Message) DeleteUser(string userId)
    {
        if (CurrentUser?.IsAdmin != true)
            return (false, "Acción permitida solo para el administrador.");

        var user = _db.UsersTable.FirstOrDefault(u => u.Id == userId);
        if (user == null)
            return (false, "Usuario no encontrado.");

        if (user.IsAdmin)
            return (false, "No se puede eliminar la cuenta principal de administrador.");

        _db.UsersTable.Remove(user);
        SaveDatabase();
        NotifyStateChanged();
        _ = MongoDb.DeleteUserAsync(userId);

        return (true, $"Usuario @{user.Username} eliminado permanentemente.");
    }

    public (bool Success, string Message) AdjustUserBalance(string userId, decimal amountDelta, string reason)
    {
        if (CurrentUser?.IsAdmin != true)
            return (false, "Acción permitida solo para el administrador.");

        var user = _db.UsersTable.FirstOrDefault(u => u.Id == userId);
        if (user == null)
            return (false, "Usuario no encontrado.");

        decimal oldBalance = user.Balance;
        user.Balance = Math.Max(0, user.Balance + amountDelta);

        _db.WalletTransactionsTable.Insert(0, new WalletTransaction
        {
            UserId = user.Id,
            Username = user.Username,
            Amount = amountDelta,
            Type = TransactionType.PlatformCommission,
            Description = $"Ajuste admin por @{CurrentUser.Username}: {reason}"
        });

        SaveDatabase();
        NotifyStateChanged();
        _ = MongoDb.UpsertUserAsync(user);

        return (true, $"Saldo de @{user.Username} ajustado de ${oldBalance:N0} a ${user.Balance:N0} COP.");
    }

    public IReadOnlyList<User> GetRealOpponents()
    {
        if (CurrentUser == null)
            return _db.UsersTable.ToList();

        return _db.UsersTable
            .Where(u => u.Id != CurrentUser.Id)
            .OrderByDescending(u => u.CreatedAt)
            .ToList();
    }

    public (bool Success, string Message, MatchChallenge? Challenge) CreateChallenge(
        string gameId,
        decimal betAmount,
        string? targetOpponentUserId = null,
        bool isPracticeBot = false)
    {
        if (CurrentUser == null)
            return (false, "Debes iniciar sesión.", null);

        var gameMeta = GameRegistry.GetGameMetadata(gameId);
        if (gameMeta == null)
            return (false, "Juego no encontrado.", null);

        if (betAmount < gameMeta.MinBet)
            return (false, $"La apuesta mínima para {gameMeta.Name} es de ${gameMeta.MinBet:N0} COP.", null);

        if (CurrentUser.Balance < betAmount)
            return (false, $"Saldo insuficiente. Tienes ${CurrentUser.Balance:N0} COP disponibles.", null);

        CurrentUser.Balance -= betAmount;
        CurrentUser.LockedBalance += betAmount;

        User? targetOpponent = null;
        if (!string.IsNullOrWhiteSpace(targetOpponentUserId) && !isPracticeBot)
        {
            targetOpponent = _db.UsersTable.FirstOrDefault(u =>
                u.Id == targetOpponentUserId ||
                u.Username.Equals(targetOpponentUserId, StringComparison.OrdinalIgnoreCase));
        }

        var challenge = new MatchChallenge
        {
            GameId = gameMeta.Id,
            GameName = gameMeta.Name,
            GameIcon = gameMeta.Icon,
            CreatorId = CurrentUser.Id,
            CreatorUsername = CurrentUser.DisplayName,
            TargetOpponentId = targetOpponent?.Id,
            TargetOpponentUsername = targetOpponent?.DisplayName,
            OpponentId = isPracticeBot ? "BOT-KITO" : null,
            OpponentUsername = isPracticeBot
                ? "🤖 Bot Kito (Práctica)"
                : (targetOpponent != null ? targetOpponent.DisplayName : "Esperando Jugador..."),
            BetAmount = betAmount,
            CommissionPercentageSnapshot = _db.CommissionPercentage,
            IsPracticeBot = isPracticeBot,
            Status = isPracticeBot ? MatchStatus.InProgress : MatchStatus.WaitingForOpponent
        };

        _db.MatchChallengesTable.Add(challenge);
        AddTransaction(
            CurrentUser,
            -betAmount,
            TransactionType.BetLocked,
            $"Apuesta en custodia para reto #{challenge.Id} ({gameMeta.Name})");

        GameRegistry.GetOrCreateMatchEngine(challenge.Id, challenge.GameId);

        // Si es partida multijugador real, generar notificación para el rival específico o para todos los jugadores registrados
        if (!isPracticeBot)
        {
            if (targetOpponent != null)
            {
                var directNotif = AddNotification(
                    targetOpponent.Id,
                    NotificationType.DirectChallengeReceived,
                    $"⚔️ ¡{CurrentUser.DisplayName} te está retando!",
                    $"{CurrentUser.DisplayName} (@{CurrentUser.Username}) te envió un reto directo en {gameMeta.Name} por ${betAmount:N0} COP (Pozo: ${challenge.TotalPot:N0} COP).",
                    challenge.Id,
                    betAmount,
                    sendSystemPush: false);
                _ = MongoDb.UpsertNotificationAsync(directNotif);
            }
            else
            {
                foreach (var otherUser in _db.UsersTable.Where(u => u.Id != CurrentUser.Id))
                {
                    var openNotif = AddNotification(
                        otherUser.Id,
                        NotificationType.PlayerSearchingOpponent,
                        $"📢 ¡{CurrentUser.DisplayName} busca rival por ${betAmount:N0} COP!",
                        $"{CurrentUser.DisplayName} (@{CurrentUser.Username}) abrió una mesa de {gameMeta.Name} apostando ${betAmount:N0} COP (Pozo: ${challenge.TotalPot:N0} COP).",
                        challenge.Id,
                        betAmount,
                        sendSystemPush: false);
                    _ = MongoDb.UpsertNotificationAsync(openNotif);
                }
            }
        }

        SaveDatabase();
        _ = MongoDb.UpsertUserAsync(CurrentUser);
        _ = MongoDb.UpsertChallengeAsync(challenge);
        NotifyStateChanged();

        string msg = isPracticeBot
            ? $"¡Partida de práctica #{challenge.Id} iniciada!"
            : (targetOpponent != null
                ? $"¡Reto #{challenge.Id} enviado a {targetOpponent.DisplayName}! En cuanto acepte en su celular, comenzará la partida."
                : $"¡Reto abierto #{challenge.Id} publicado en la nube! Cualquier jugador conectado puede aceptarlo.");

        return (true, msg, challenge);
    }

    /// <summary>
    /// Recibe un reto directo o una alerta de un jugador buscando rival por cierto monto, generando notificación en tiempo real.
    /// </summary>
    public (bool Success, string Message, MatchChallenge? Challenge, AppNotification? Notification) ReceiveNewIncomingChallenge(
        string gameId,
        decimal betAmount = 1000m,
        bool isDirectChallengeToMe = true)
    {
        if (CurrentUser == null)
            return (false, "Inicia sesión primero.", null, null);

        var otherRealUser = _db.UsersTable.FirstOrDefault(u => u.Id != CurrentUser.Id);
        string rivalId = otherRealUser?.Id ?? "BOT-KITO";
        string rivalName = otherRealUser?.DisplayName ?? "Jugador Kito";
        var gameMeta = GameRegistry.GetGameMetadata(gameId) ?? GameRegistry.GetAvailableGames().First();

        var challenge = new MatchChallenge
        {
            GameId = gameMeta.Id,
            GameName = gameMeta.Name,
            GameIcon = gameMeta.Icon,
            CreatorId = rivalId,
            CreatorUsername = rivalName,
            TargetOpponentId = isDirectChallengeToMe ? CurrentUser.Id : null,
            TargetOpponentUsername = isDirectChallengeToMe ? CurrentUser.DisplayName : null,
            BetAmount = betAmount,
            CommissionPercentageSnapshot = _db.CommissionPercentage,
            IsPracticeBot = otherRealUser == null,
            Status = MatchStatus.WaitingForOpponent
        };

        _db.MatchChallengesTable.Add(challenge);

        string notifTitle = isDirectChallengeToMe
            ? $"⚔️ ¡{rivalName} te está retando!"
            : $"📢 ¡{rivalName} busca rival por ${betAmount:N0} COP!";

        string notifBody = isDirectChallengeToMe
            ? $"{rivalName} te envió un reto directo en {gameMeta.Name} apostando ${betAmount:N0} COP (Pozo: ${challenge.TotalPot:N0} COP)."
            : $"{rivalName} está buscando quién lo rete en {gameMeta.Name} por un monto de ${betAmount:N0} COP (Pozo: ${challenge.TotalPot:N0} COP).";

        var notif = AddNotification(
            CurrentUser.Id,
            isDirectChallengeToMe ? NotificationType.DirectChallengeReceived : NotificationType.PlayerSearchingOpponent,
            notifTitle,
            notifBody,
            challenge.Id,
            betAmount);

        SaveDatabase();
        _ = MongoDb.UpsertChallengeAsync(challenge);
        _ = MongoDb.UpsertNotificationAsync(notif);
        NotifyStateChanged();

        if (CurrentUser.NotificationsEnabled && notif != null)
        {
            NotificationTriggered?.Invoke(notif);
        }

        return (true, notifBody, challenge, notif);
    }

    public (bool Success, string Message) AcceptChallenge(string challengeId)
    {
        if (CurrentUser == null)
            return (false, "Inicia sesión para aceptar un reto.");

        var challenge = _db.MatchChallengesTable.FirstOrDefault(c => c.Id == challengeId);
        if (challenge == null)
            return (false, "El reto no existe.");

        // Si el creador del reto toca su propio reto en espera, entra directamente al tablero a esperar al rival
        if (challenge.CreatorId == CurrentUser.Id)
        {
            GameRegistry.GetOrCreateMatchEngine(challenge.Id, challenge.GameId);
            return (true, "Entrando a tu sala de juego...");
        }

        if (challenge.Status == MatchStatus.InProgress &&
            (challenge.CreatorId == CurrentUser.Id || challenge.OpponentId == CurrentUser.Id))
        {
            GameRegistry.GetOrCreateMatchEngine(challenge.Id, challenge.GameId);
            return (true, "Reanudando partida en curso...");
        }

        if (challenge.Status != MatchStatus.WaitingForOpponent)
            return (false, "Este reto ya fue tomado por otro jugador o ya finalizó.");

        if (!string.IsNullOrEmpty(challenge.TargetOpponentId) && challenge.TargetOpponentId != CurrentUser.Id)
            return (false, $"Este reto es directo para {challenge.TargetOpponentUsername}.");

        if (CurrentUser.Balance < challenge.BetAmount)
            return (false, $"Necesitas ${challenge.BetAmount:N0} COP de saldo disponible para aceptar este reto.");

        CurrentUser.Balance -= challenge.BetAmount;
        CurrentUser.LockedBalance += challenge.BetAmount;

        // En multijugador real, CreatorId se mantiene como Player1 y quien acepta es OpponentId (Player2)
        challenge.OpponentId = CurrentUser.Id;
        challenge.OpponentUsername = CurrentUser.DisplayName;
        challenge.CommissionPercentageSnapshot = _db.CommissionPercentage;
        challenge.Status = MatchStatus.InProgress;

        // Marcar notificación asociada como leída
        foreach (var n in _db.NotificationsTable.Where(n => n.RelatedChallengeId == challenge.Id))
        {
            n.IsRead = true;
        }

        AddTransaction(
            CurrentUser,
            -challenge.BetAmount,
            TransactionType.BetLocked,
            $"Aceptaste reto #{challenge.Id} vs {challenge.CreatorUsername} en {challenge.GameName}");

        GameRegistry.GetOrCreateMatchEngine(challenge.Id, challenge.GameId);

        SaveDatabase();
        _ = MongoDb.UpsertUserAsync(CurrentUser);
        _ = MongoDb.UpsertChallengeAsync(challenge);
        NotifyStateChanged();
        return (true, $"¡Apuesta igualada contra {challenge.CreatorUsername}! Pozo total en custodia: ${challenge.TotalPot:N0} COP.");
    }

    public async Task RecordMoveAndSyncAsync(string challengeId, BoardPosition from, BoardPosition to)
    {
        var challenge = _db.MatchChallengesTable.FirstOrDefault(c => c.Id == challengeId);
        if (challenge == null) return;

        string moveToken = $"{from.Row},{from.Col}->{to.Row},{to.Col}";
        challenge.SerializedMoves.Add(moveToken);
        SaveDatabase();
        await MongoDb.UpsertChallengeAsync(challenge);
    }

    public async Task<MatchChallenge?> PullLiveChallengeStateAsync(string challengeId)
    {
        var remote = await MongoDb.GetChallengeByIdAsync(challengeId);
        if (remote != null)
        {
            var idx = _db.MatchChallengesTable.FindIndex(c => c.Id == challengeId);
            if (idx >= 0)
            {
                _db.MatchChallengesTable[idx] = remote;
            }
            else
            {
                _db.MatchChallengesTable.Add(remote);
            }
            return remote;
        }
        return _db.MatchChallengesTable.FirstOrDefault(c => c.Id == challengeId);
    }

    public (bool Success, string SummaryMessage) SettleMatchResult(string challengeId, PlayerSide winnerSide, bool isDraw)
    {
        var challenge = _db.MatchChallengesTable.FirstOrDefault(c => c.Id == challengeId);
        if (challenge == null || CurrentUser == null)
            return (false, "Partida no encontrada.");

        if (challenge.Status == MatchStatus.Completed || challenge.Status == MatchStatus.Draw)
        {
            // Asegurar que los datos del usuario actual estén sincronizados si el otro celular ya liquidó la partida
            _ = SyncWithMongoAtlasAsync();
            string existingSummary = challenge.Status == MatchStatus.Draw
                ? $"Partida en Tablas. Se reembolsaron ${challenge.BetAmount:N0} COP a cada jugador."
                : $"🏆 Ganador de la partida: {challenge.WinnerUsername} (Premio: ${challenge.WinnerPrize:N0} COP).";
            return (true, existingSummary);
        }

        var creatorUser = _db.UsersTable.FirstOrDefault(u => u.Id == challenge.CreatorId);
        var opponentUser = _db.UsersTable.FirstOrDefault(u => u.Id == challenge.OpponentId);

        if (creatorUser != null)
            creatorUser.LockedBalance = Math.Max(0, creatorUser.LockedBalance - challenge.BetAmount);

        if (opponentUser != null)
            opponentUser.LockedBalance = Math.Max(0, opponentUser.LockedBalance - challenge.BetAmount);

        challenge.CompletedAt = DateTime.Now;

        if (isDraw || winnerSide == PlayerSide.None)
        {
            challenge.Status = MatchStatus.Draw;

            if (creatorUser != null)
            {
                creatorUser.Balance += challenge.BetAmount;
                AddTransaction(creatorUser, challenge.BetAmount, TransactionType.BetRefund, $"Empate en {challenge.GameName} (#{challenge.Id}) - Apuesta reembolsada");
                _ = MongoDb.UpsertUserAsync(creatorUser);
            }

            if (opponentUser != null)
            {
                opponentUser.Balance += challenge.BetAmount;
                AddTransaction(opponentUser, challenge.BetAmount, TransactionType.BetRefund, $"Empate en {challenge.GameName} (#{challenge.Id}) - Apuesta reembolsada");
                _ = MongoDb.UpsertUserAsync(opponentUser);
            }

            SaveDatabase();
            _ = MongoDb.UpsertChallengeAsync(challenge);
            NotifyStateChanged();
            return (true, $"Partida en Tablas. Se reembolsaron ${challenge.BetAmount:N0} COP a cada jugador.");
        }

        decimal totalPot = challenge.TotalPot;
        decimal commissionRate = _db.CommissionPercentage;
        decimal commissionAmount = Math.Round(totalPot * (commissionRate / 100m), 0);
        decimal winnerPrize = totalPot - commissionAmount;

        challenge.Status = MatchStatus.Completed;
        challenge.CommissionPercentageSnapshot = commissionRate;
        challenge.CommissionAmount = commissionAmount;
        challenge.WinnerPrize = winnerPrize;
        _db.PlatformTreasuryBalance += commissionAmount;

        if (winnerSide == PlayerSide.Player1)
        {
            challenge.WinnerUserId = challenge.CreatorId;
            challenge.WinnerUsername = challenge.CreatorUsername;

            if (creatorUser != null)
            {
                creatorUser.Balance += winnerPrize;
                creatorUser.Wins++;
                AddTransaction(creatorUser, winnerPrize, TransactionType.PrizeWon, $"¡Ganaste partida #{challenge.Id} ({challenge.GameName})! Pozo: ${totalPot:N0} | Comisión ({commissionRate:0.#}%): ${commissionAmount:N0}");
                _ = MongoDb.UpsertUserAsync(creatorUser);
            }

            if (opponentUser != null)
            {
                opponentUser.Losses++;
                _ = MongoDb.UpsertUserAsync(opponentUser);
            }
        }
        else
        {
            challenge.WinnerUserId = challenge.OpponentId ?? "BOT-KITO";
            challenge.WinnerUsername = challenge.OpponentUsername ?? "Contrincante";

            if (opponentUser != null)
            {
                opponentUser.Balance += winnerPrize;
                opponentUser.Wins++;
                AddTransaction(opponentUser, winnerPrize, TransactionType.PrizeWon, $"¡Ganaste partida #{challenge.Id} ({challenge.GameName})! Pozo: ${totalPot:N0} | Comisión ({commissionRate:0.#}%): ${commissionAmount:N0}");
                _ = MongoDb.UpsertUserAsync(opponentUser);
            }

            if (creatorUser != null)
            {
                creatorUser.Losses++;
                _ = MongoDb.UpsertUserAsync(creatorUser);
            }
        }

        SaveDatabase();
        _ = MongoDb.UpsertChallengeAsync(challenge);
        NotifyStateChanged();

        bool didIWin = challenge.WinnerUserId == CurrentUser.Id;
        return didIWin
            ? (true, $"🎉 ¡Victoria! Ganaste la partida y recibiste ${winnerPrize:N0} COP en tu billetera (Pozo: ${totalPot:N0} - Comisión {commissionRate:0.#}%: ${commissionAmount:N0}).")
            : (true, $"🏆 {challenge.WinnerUsername} ganó la partida #{challenge.Id} y se llevó el pozo de ${winnerPrize:N0} COP.");
    }

    public void ResetTemporaryDatabase()
    {
        if (File.Exists(_dbFilePath))
        {
            File.Delete(_dbFilePath);
        }
        InitializeCleanDatabase();
        NotifyStateChanged();
    }

    private AppNotification AddNotification(
        string userId,
        NotificationType type,
        string title,
        string message,
        string? relatedChallengeId,
        decimal? betAmount,
        bool sendSystemPush = true)
    {
        var notif = new AppNotification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            RelatedChallengeId = relatedChallengeId,
            BetAmount = betAmount,
            IsRead = false,
            CreatedAt = DateTime.Now
        };
        _db.NotificationsTable.Add(notif);

        if (sendSystemPush && (CurrentUser == null || CurrentUser.NotificationsEnabled))
        {
            NativeNotificationService.ShowSystemNotification(notif);
        }

        return notif;
    }

    private void AddTransaction(User user, decimal amount, TransactionType type, string description)
    {
        _db.WalletTransactionsTable.Add(new WalletTransaction
        {
            UserId = user.Id,
            Username = user.DisplayName,
            Amount = amount,
            Type = type,
            Description = description,
            Timestamp = DateTime.Now
        });
    }

    private void LoadOrInitializeDatabase()
    {
        try
        {
            if (File.Exists(_dbFilePath))
            {
                string json = File.ReadAllText(_dbFilePath);
                var loaded = JsonSerializer.Deserialize<TemporaryDatabaseSchema>(json);
                if (loaded != null && loaded.UsersTable.Count > 0)
                {
                    _db = loaded;
                    CurrentUser = _db.UsersTable.FirstOrDefault(u => u.Id == _db.LastLoggedInUserId)
                                  ?? _db.UsersTable.FirstOrDefault();
                    return;
                }
            }
        }
        catch
        {
            // Inicializar base de datos limpia
        }

        InitializeCleanDatabase();
    }

    private void InitializeCleanDatabase()
    {
        _db = new TemporaryDatabaseSchema
        {
            LanguageCode = "es",
            CommissionPercentage = 10m,
            PlatformTreasuryBalance = 0m
        };

        var mainUser = new User
        {
            Id = "user-ivan-01",
            FirstName = "Ivan",
            LastName = "Pautt",
            Email = "ivan@kitogame.com",
            Phone = "3000000000",
            Username = "ivan",
            Password = "1234",
            Balance = 5000m,
            LockedBalance = 0m,
            IsAdmin = true,
            NotificationsEnabled = true
        };

        _db.UsersTable.Add(mainUser);
        _db.LastLoggedInUserId = mainUser.Id;
        CurrentUser = mainUser;

        AddTransaction(mainUser, 5000m, TransactionType.Deposit, "Saldo inicial confirmado en base de datos temporal");
        EnsureSampleIncomingChallengesForUser(mainUser);
        SaveDatabase();
    }

    public static bool IsMasterAdminAccount(string? username, string? email)
    {
        if (!string.IsNullOrWhiteSpace(username) &&
            username.Trim().Equals("ivan", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(email))
        {
            string e = email.Trim();
            if (e.Equals("ivan@kitogame.com", StringComparison.OrdinalIgnoreCase) ||
                e.Equals("ivan.pautt@gmail.com", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void EnsureSampleIncomingChallengesForUser(User user)
    {
        user.IsAdmin = IsMasterAdminAccount(user.Username, user.Email);
        foreach (var u in _db.UsersTable)
        {
            u.IsAdmin = IsMasterAdminAccount(u.Username, u.Email);
        }

        // Limpiar retos simulados antiguos para que solo existan usuarios y retos reales
        _db.MatchChallengesTable.RemoveAll(c => c.CreatorId == "RIVAL-ONLINE" && !c.IsPracticeBot);
        _db.NotificationsTable.RemoveAll(n => n.Message.Contains("@Carlos_M") || n.Message.Contains("@Andres_G"));

        // Garantizar que cualquier usuario nuevo/existente sin movimientos tenga su saldo inicial de $5.000 COP
        if (user.Balance == 0m && user.LockedBalance == 0m && user.Wins == 0 && user.Losses == 0)
        {
            user.Balance = 5000m;
            AddTransaction(user, 5000m, TransactionType.Deposit, "Saldo inicial de $5.000 COP asignado");
        }
    }

    private void SaveDatabase()
    {
        try
        {
            string json = JsonSerializer.Serialize(_db, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dbFilePath, json);
        }
        catch
        {
            // Ignorar errores de archivo en modo temporal
        }

        _ = PushChangesToMongoAtlasAsync();
    }

    public async Task<bool> SyncWithMongoAtlasAsync()
    {
        try
        {
            bool connected = await MongoDb.PingAsync();
            if (!connected)
                return false;

            await MongoDb.DeleteLegacySimulatedChallengesAsync();
            _db.MatchChallengesTable.RemoveAll(c => c.CreatorId == "RIVAL-ONLINE" && !c.IsPracticeBot);

            // 1. Configuración global de la plataforma
            if (MongoDb.PlatformSettings != null)
            {
                var remoteSettings = await MongoDb.PlatformSettings
                    .Find(s => s.Id == "global_settings")
                    .FirstOrDefaultAsync();

                if (remoteSettings != null)
                {
                    _db.CommissionPercentage = remoteSettings.CommissionPercentage;
                    _db.PlatformTreasuryBalance = Math.Max(_db.PlatformTreasuryBalance, remoteSettings.PlatformTreasuryBalance);
                }
                else
                {
                    await MongoDb.UpsertSettingsAsync(_db.CommissionPercentage, _db.PlatformTreasuryBalance);
                }
            }

            // 2. Usuarios (Sincronización bidireccional y saldo de $5.000 COP para todos los nuevos)
            if (MongoDb.Users != null)
            {
                var remoteUsers = await MongoDb.Users.Find(_ => true).ToListAsync();
                foreach (var ru in remoteUsers)
                {
                    bool shouldBeAdmin = IsMasterAdminAccount(ru.Username, ru.Email);
                    bool changed = false;

                    if (ru.IsAdmin != shouldBeAdmin)
                    {
                        ru.IsAdmin = shouldBeAdmin;
                        changed = true;
                    }

                    if (ru.Balance == 0m && ru.LockedBalance == 0m && ru.Wins == 0 && ru.Losses == 0)
                    {
                        ru.Balance = 5000m;
                        changed = true;
                    }

                    if (changed)
                    {
                        await MongoDb.UpsertUserAsync(ru);
                    }

                    var localIdx = _db.UsersTable.FindIndex(u => u.Id == ru.Id || u.Email.Equals(ru.Email, StringComparison.OrdinalIgnoreCase));
                    if (localIdx >= 0)
                    {
                        _db.UsersTable[localIdx] = ru;
                    }
                    else
                    {
                        _db.UsersTable.Add(ru);
                    }
                }

                foreach (var lu in _db.UsersTable.ToList())
                {
                    if (lu.Balance == 0m && lu.LockedBalance == 0m && lu.Wins == 0 && lu.Losses == 0)
                    {
                        lu.Balance = 5000m;
                    }
                    if (!remoteUsers.Any(r => r.Id == lu.Id))
                    {
                        await MongoDb.UpsertUserAsync(lu);
                    }
                }

                if (CurrentUser != null)
                {
                    CurrentUser = _db.UsersTable.FirstOrDefault(u => u.Id == CurrentUser.Id) ?? CurrentUser;
                }
            }

            // 3. Retos y Partidas 1v1 en tiempo real
            if (MongoDb.MatchChallenges != null)
            {
                var remoteChallenges = await MongoDb.MatchChallenges.Find(_ => true).ToListAsync();
                foreach (var rc in remoteChallenges)
                {
                    var localIdx = _db.MatchChallengesTable.FindIndex(c => c.Id == rc.Id);
                    if (localIdx >= 0)
                    {
                        _db.MatchChallengesTable[localIdx] = rc;
                    }
                    else
                    {
                        _db.MatchChallengesTable.Add(rc);
                    }
                }

                foreach (var lc in _db.MatchChallengesTable.ToList())
                {
                    if (!remoteChallenges.Any(r => r.Id == lc.Id))
                    {
                        await MongoDb.UpsertChallengeAsync(lc);
                    }
                }
            }

            // 4. Notificaciones en tiempo real desde otros teléfonos
            if (MongoDb.Notifications != null && CurrentUser != null)
            {
                var remoteNotifs = await MongoDb.Notifications
                    .Find(n => n.UserId == CurrentUser.Id)
                    .ToListAsync();

                foreach (var rn in remoteNotifs)
                {
                    if (!_db.NotificationsTable.Any(n => n.Id == rn.Id))
                    {
                        _db.NotificationsTable.Add(rn);
                        if (!rn.IsRead && CurrentUser.NotificationsEnabled)
                        {
                            NativeNotificationService.ShowSystemNotification(rn);
                        }
                    }
                }
            }

            // 5. Órdenes de Pago
            if (MongoDb.PaymentRequests != null)
            {
                var remotePayments = await MongoDb.PaymentRequests.Find(_ => true).ToListAsync();
                foreach (var rp in remotePayments)
                {
                    var localIdx = _db.PaymentRequestsTable.FindIndex(p => p.Id == rp.Id);
                    if (localIdx >= 0)
                    {
                        _db.PaymentRequestsTable[localIdx] = rp;
                    }
                    else
                    {
                        _db.PaymentRequestsTable.Add(rp);
                    }
                }
            }

            // 6. Movimientos de Billetera
            if (MongoDb.WalletTransactions != null)
            {
                var remoteTxs = await MongoDb.WalletTransactions.Find(_ => true).ToListAsync();
                foreach (var rt in remoteTxs)
                {
                    if (!_db.WalletTransactionsTable.Any(t => t.Id == rt.Id))
                    {
                        _db.WalletTransactionsTable.Add(rt);
                    }
                }
            }

            string json = JsonSerializer.Serialize(_db, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dbFilePath, json);

            MainThread.BeginInvokeOnMainThread(NotifyStateChanged);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task PushChangesToMongoAtlasAsync()
    {
        try
        {
            if (CurrentUser != null)
            {
                await MongoDb.UpsertUserAsync(CurrentUser);
            }

            await MongoDb.UpsertSettingsAsync(_db.CommissionPercentage, _db.PlatformTreasuryBalance);

            var recentChallenge = _db.MatchChallengesTable.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
            if (recentChallenge != null)
            {
                await MongoDb.UpsertChallengeAsync(recentChallenge);
            }

            var recentPayment = _db.PaymentRequestsTable.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
            if (recentPayment != null)
            {
                await MongoDb.UpsertPaymentRequestAsync(recentPayment);
            }

            var recentTx = _db.WalletTransactionsTable.OrderByDescending(t => t.Timestamp).FirstOrDefault();
            if (recentTx != null)
            {
                await MongoDb.UpsertTransactionAsync(recentTx);
            }
        }
        catch
        {
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}

