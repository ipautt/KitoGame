using KitoGame.AdminWeb.Services;
using KitoGame.Shared.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KitoGame.AdminWeb.Pages;

public class IndexModel : PageModel
{
    private readonly AdminDataService _dataService;

    public DashboardStats Stats { get; set; } = new();
    public List<User> RecentUsers { get; set; } = new();
    public List<PaymentRequest> RecentPayments { get; set; } = new();

    public IndexModel(AdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task OnGetAsync()
    {
        Stats = await _dataService.GetDashboardStatsAsync();
        var allUsers = await _dataService.GetUsersAsync();
        RecentUsers = allUsers.Take(6).ToList();

        var allPayments = await _dataService.GetPaymentRequestsAsync();
        RecentPayments = allPayments.Take(5).ToList();
    }
}
