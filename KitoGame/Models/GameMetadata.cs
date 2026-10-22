namespace KitoGame.Models;

public class GameMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string RulesSummary { get; set; } = string.Empty;
    public decimal MinBet { get; set; } = 1000m;
    public bool IsEnabled { get; set; } = true;
    public string AccentColorHex { get; set; } = "#6366F1";

    public string FormattedMinBet => $"Apuesta mín: ${MinBet:N0} COP";
}

