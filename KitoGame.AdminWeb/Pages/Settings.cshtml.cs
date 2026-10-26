using KitoGame.AdminWeb.Services;
using KitoGame.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KitoGame.AdminWeb.Pages;

public class SettingsModel : PageModel
{
    private readonly AdminDataService _dataService;

    [BindProperty]
    public SystemConfig Config { get; set; } = new();

    [BindProperty]
    public bool CheckersEnabled { get; set; } = true;

    [BindProperty]
    public bool ChessEnabled { get; set; } = true;

    [BindProperty]
    public bool ParchisEnabled { get; set; } = true;

    public bool IsConnected => _dataService.IsConnected;
    public string ConnectionStatusMessage => _dataService.ConnectionStatusMessage;

    public SettingsModel(AdminDataService dataService)
    {
        _dataService = dataService;
    }

    public void OnGet()
    {
        Config = _dataService.CurrentConfig;
        CheckersEnabled = !Config.DisabledGameIds.Contains("checkers");
        ChessEnabled = !Config.DisabledGameIds.Contains("chess");
        ParchisEnabled = !Config.DisabledGameIds.Contains("parchis");
    }

    public async Task<IActionResult> OnPostSaveDatabaseAsync()
    {
        string admin = User.Identity?.Name ?? "ivan";

        // Probar conexión y guardar parámetros
        var res = await _dataService.UpdateDatabaseConfigAsync(Config, admin);

        if (res.Success)
        {
            TempData["SuccessMessage"] = res.Message;
        }
        else
        {
            TempData["ErrorMessage"] = res.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveRulesAsync()
    {
        string admin = User.Identity?.Name ?? "ivan";

        // Conservar parámetros de conexión actuales y actualizar reglas
        var current = _dataService.CurrentConfig;
        current.CommissionPercentage = Config.CommissionPercentage;
        current.WelcomeBonusAmount = Config.WelcomeBonusAmount;
        current.MinBetAmount = Config.MinBetAmount;
        current.MaxBetAmount = Config.MaxBetAmount;
        current.MaintenanceMode = Config.MaintenanceMode;
        current.MaintenanceMessage = Config.MaintenanceMessage;

        current.DisabledGameIds = new List<string>();
        if (!CheckersEnabled) current.DisabledGameIds.Add("checkers");
        if (!ChessEnabled) current.DisabledGameIds.Add("chess");
        if (!ParchisEnabled) current.DisabledGameIds.Add("parchis");

        current.AutoPaymentsModuleEnabled = Config.AutoPaymentsModuleEnabled;
        current.WithdrawalHoldHours = Config.WithdrawalHoldHours > 0 ? Config.WithdrawalHoldHours : 48;

        var res = await _dataService.UpdateDatabaseConfigAsync(current, admin);

        if (res.Success)
            TempData["SuccessMessage"] = "Reglas de la plataforma guardadas correctamente.";
        else
            TempData["ErrorMessage"] = res.Message;

        return RedirectToPage();
    }
}

