using MongoDB.Bson.Serialization.Attributes;

namespace KitoGame.Models;

[BsonIgnoreExtraElements]
public class User
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AuthProvider { get; set; } = "Local"; // "Local" o "Google"

    // Datos de Cuenta Inscrita para Retiro de Ganancias
    public string PayoutBankOrWallet { get; set; } = string.Empty; // Ej: Nequi, Daviplata, Bancolombia, Davivienda
    public string PayoutAccountType { get; set; } = string.Empty;  // Ej: Billetera Móvil, Ahorros, Corriente
    public string PayoutAccountNumber { get; set; } = string.Empty; // Número de celular o cuenta bancaria
    public string PayoutDocumentId { get; set; } = string.Empty;    // Cédula / Documento del titular
    public bool NotificationsEnabled { get; set; } = true;

    public decimal Balance { get; set; }
    public decimal LockedBalance { get; set; }
    public bool IsAdmin { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string FullName => $"{FirstName} {LastName}".Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Username : FullName;
    public string RoleDisplay => IsAdmin ? "Administrador" : "Jugador";
    public string FormattedBalance => $"${Balance:N0} COP";
    public string FormattedLockedBalance => $"${LockedBalance:N0} COP";

    public bool HasRegisteredPayoutAccount =>
        !string.IsNullOrWhiteSpace(PayoutBankOrWallet) &&
        !string.IsNullOrWhiteSpace(PayoutAccountNumber);

    public string PayoutAccountSummary => HasRegisteredPayoutAccount
        ? $"{PayoutBankOrWallet} ({PayoutAccountType}) • Nro: {PayoutAccountNumber} • Doc: {PayoutDocumentId}"
        : "⚠️ Sin cuenta inscrita para retiros";
}

