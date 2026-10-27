using KitoGame.AdminWeb.Services;
using KitoGame.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KitoGame.AdminWeb.Pages;

public class UsersModel : PageModel
{
    private readonly AdminDataService _dataService;

    public List<User> Users { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? OnlyBanned { get; set; }

    public UsersModel(AdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task OnGetAsync()
    {
        Users = await _dataService.GetUsersAsync(Search, OnlyBanned);
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string userId, string newPassword)
    {
        string admin = User.Identity?.Name ?? "admin";
        var res = await _dataService.ResetPasswordAsync(userId, newPassword, admin);

        if (res.Success)
            TempData["SuccessMessage"] = res.Message;
        else
            TempData["ErrorMessage"] = res.Message;

        return RedirectToPage(new { search = Search, onlyBanned = OnlyBanned });
    }

    public async Task<IActionResult> OnPostAdjustBalanceAsync(string userId, decimal amountDelta, string reason, bool isExactSet = false)
    {
        string admin = User.Identity?.Name ?? "admin";
        var res = await _dataService.AdjustUserBalanceAsync(userId, amountDelta, reason, admin, isExactSet);

        if (res.Success)
            TempData["SuccessMessage"] = res.Message;
        else
            TempData["ErrorMessage"] = res.Message;

        return RedirectToPage(new { search = Search, onlyBanned = OnlyBanned });
    }

    public async Task<IActionResult> OnPostToggleBanAsync(string userId, string reason)
    {
        string admin = User.Identity?.Name ?? "admin";
        var res = await _dataService.ToggleUserBanAsync(userId, reason, admin);

        if (res.Success)
            TempData["SuccessMessage"] = res.Message;
        else
            TempData["ErrorMessage"] = res.Message;

        return RedirectToPage(new { search = Search, onlyBanned = OnlyBanned });
    }

    public async Task<IActionResult> OnPostDeleteUserAsync(string userId)
    {
        string admin = User.Identity?.Name ?? "admin";
        var res = await _dataService.DeleteUserAsync(userId, admin);

        if (res.Success)
            TempData["SuccessMessage"] = res.Message;
        else
            TempData["ErrorMessage"] = res.Message;

        return RedirectToPage(new { search = Search, onlyBanned = OnlyBanned });
    }
}

