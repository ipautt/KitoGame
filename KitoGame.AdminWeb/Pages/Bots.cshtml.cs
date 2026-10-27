using KitoGame.AdminWeb.Services;
using KitoGame.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KitoGame.AdminWeb.Pages;

public class BotsModel : PageModel
{
    private readonly AdminDataService _dataService;

    public List<SystemBot> Bots { get; set; } = new();
    public List<User> RealUsers { get; set; } = new();

    public BotsModel(AdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task OnGetAsync()
    {
        Bots = await _dataService.GetBotsAsync();
        var allUsers = await _dataService.GetUsersAsync();
        // Filtrar usuarios reales
        RealUsers = allUsers.Where(u => !u.IsBot && !u.IsAdmin).ToList();
        if (!RealUsers.Any())
        {
            RealUsers = allUsers.Where(u => !u.IsBot).ToList();
        }
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(string botId)
    {
        string admin = User.Identity?.Name ?? "admin";
        var res = await _dataService.ToggleBotStatusAsync(botId, admin);

        if (res.Success) TempData["SuccessMessage"] = res.Message;
        else TempData["ErrorMessage"] = res.Message;

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateBotAsync(
        string botId,
        string name,
        bool isEnabled,
        BotSkillLevel skillLevel,
        string playStyle,
        string favoriteGame)
    {
        string admin = User.Identity?.Name ?? "admin";
        var res = await _dataService.UpdateBotAsync(botId, name, isEnabled, skillLevel, playStyle, favoriteGame, admin);

        if (res.Success) TempData["SuccessMessage"] = res.Message;
        else TempData["ErrorMessage"] = res.Message;

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateMatchAsync(
        string botId,
        string targetUserId,
        string gameId,
        decimal betAmount,
        bool botStartsFirst)
    {
        string admin = User.Identity?.Name ?? "admin";
        var res = await _dataService.CreateBotMatchAgainstUserAsync(botId, targetUserId, gameId, betAmount, botStartsFirst, admin);

        if (res.Success) TempData["SuccessMessage"] = res.Message;
        else TempData["ErrorMessage"] = res.Message;

        return RedirectToPage();
    }
}

