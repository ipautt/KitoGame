using KitoGame.AdminWeb.Services;
using KitoGame.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Driver;

namespace KitoGame.AdminWeb.Pages;

public class MatchesModel : PageModel
{
    private readonly AdminDataService _dataService;

    public List<MatchChallenge> Matches { get; set; } = new();
    public string? StatusFilter { get; set; }
    public string? Search { get; set; }

    public long TotalCount { get; set; }
    public long WaitingCount { get; set; }
    public long ActiveCount { get; set; }
    public long FinishedCount { get; set; }
    public long BotCount { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }
    [TempData]
    public string? StatusError { get; set; }

    public MatchesModel(AdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task OnGetAsync(string? status, string? search)
    {
        StatusFilter = status;
        Search = search;

        await LoadCountsAsync();
        Matches = await _dataService.GetChallengesAsync(status, search);
    }

    private async Task LoadCountsAsync()
    {
        if (_dataService.ChallengesCollection == null) return;
        try
        {
            TotalCount = await _dataService.ChallengesCollection.CountDocumentsAsync(FilterDefinition<MatchChallenge>.Empty);
            WaitingCount = await _dataService.ChallengesCollection.CountDocumentsAsync(m => m.Status == MatchStatus.WaitingForOpponent);
            ActiveCount = await _dataService.ChallengesCollection.CountDocumentsAsync(m => m.Status == MatchStatus.Active);
            FinishedCount = await _dataService.ChallengesCollection.CountDocumentsAsync(m => m.Status == MatchStatus.Completed || m.Status == MatchStatus.Cancelled || m.Status == MatchStatus.Draw);
            BotCount = await _dataService.ChallengesCollection.CountDocumentsAsync(m => m.IsPracticeBot);
        }
        catch { }
    }

    public async Task<IActionResult> OnPostDeleteMatchAsync(string challengeId, bool refundBet = true)
    {
        string admin = User.Identity?.Name ?? "Admin";
        var res = await _dataService.DeleteChallengeAsync(challengeId, refundBet, admin);
        if (res.Success)
            StatusMessage = res.Message;
        else
            StatusError = res.Message;

        return RedirectToPage(new { status = StatusFilter, search = Search });
    }

    public async Task<IActionResult> OnPostPurgeMatchesAsync(string purgeMode, bool refundWaiting = true)
    {
        string admin = User.Identity?.Name ?? "Admin";
        var res = await _dataService.PurgeTestChallengesAsync(purgeMode, refundWaiting, admin);
        if (res.Success)
            StatusMessage = res.Message;
        else
            StatusError = res.Message;

        return RedirectToPage();
    }
}
