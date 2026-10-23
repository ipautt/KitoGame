namespace KitoGame.Services;

/// <summary>
/// Proveedor seguro de secretos y conexión de base de datos para KitoGame.
/// En control de versiones (Git), este archivo no contiene usuarios, contraseñas ni URLs sensibles.
/// En entornos locales/privados de compilación, se complementa con MongoDbSecretsProvider.local.cs (ignorado por Git).
/// </summary>
public static partial class MongoDbSecretsProvider
{
    static partial void ConfigureSecrets(ref string connectionString);

    public static string GetActiveConnectionString()
    {
        // 1. Variable de entorno del sistema operativo
        string? envUri = Environment.GetEnvironmentVariable("KITOGAME_MONGODB_URI")
                      ?? Environment.GetEnvironmentVariable("MONGODB_URI");
        if (!string.IsNullOrWhiteSpace(envUri))
            return envUri.Trim();

        // 2. Archivo local protegido en el dispositivo (ignorado por Git)
        try
        {
            string localFilePath = Path.Combine(FileSystem.AppDataDirectory, "mongo_secrets.json");
            if (File.Exists(localFilePath))
            {
                string json = File.ReadAllText(localFilePath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ConnectionString", out var prop) && !string.IsNullOrWhiteSpace(prop.GetString()))
                    return prop.GetString()!.Trim();
            }
        }
        catch { }

        // 3. Proveedor local privado (implementado en MongoDbSecretsProvider.local.cs, ignorado por Git)
        string conn = string.Empty;
        ConfigureSecrets(ref conn);
        return conn;
    }
}

