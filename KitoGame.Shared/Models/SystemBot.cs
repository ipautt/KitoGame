using MongoDB.Bson.Serialization.Attributes;

namespace KitoGame.Shared.Models;

public enum BotSkillLevel
{
    Principiante = 1,
    Intermedio = 2,
    Avanzado = 3,
    Maestro = 4,
    GranMaestro = 5
}

[BsonIgnoreExtraElements]
public class SystemBot
{
    [BsonId]
    public string Id { get; set; } = string.Empty; // "bot_1", "bot_2", "bot_3", "bot_4", "bot_5"
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarEmoji { get; set; } = "🤖";
    public string ColorHex { get; set; } = "#F59E0B";
    public bool IsEnabled { get; set; } = true;
    public BotSkillLevel SkillLevel { get; set; } = BotSkillLevel.GranMaestro;
    public string PlayStyle { get; set; } = "Estratégico Implacable";
    public string Description { get; set; } = string.Empty;
    public string FavoriteGame { get; set; } = "Todos";
    public decimal VirtualBalance { get; set; } = 1_000_000m;

    public int MatchesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastPlayedAt { get; set; }

    [BsonIgnore]
    public string StatusBadge => IsEnabled ? "🟢 Activo" : "🔴 Desactivado";

    [BsonIgnore]
    public string SkillLevelName => SkillLevel switch
    {
        BotSkillLevel.Principiante => "Principiante (Nivel 1)",
        BotSkillLevel.Intermedio => "Intermedio (Nivel 2)",
        BotSkillLevel.Avanzado => "Avanzado (Nivel 3)",
        BotSkillLevel.Maestro => "Maestro Táctico (Nivel 4)",
        BotSkillLevel.GranMaestro => "Gran Maestro Supremo (Nivel 5)",
        _ => "Gran Maestro"
    };

    [BsonIgnore]
    public double WinRate => MatchesPlayed > 0 ? Math.Round((double)Wins / MatchesPlayed * 100, 1) : 0;
}
