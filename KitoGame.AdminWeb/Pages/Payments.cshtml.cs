using KitoGame.AdminWeb.Services;
using KitoGame.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KitoGame.AdminWeb.Pages;

public class PaymentsModel : PageModel
{
    private readonly AdminDataService _dataService;

    public List<PaymentRequest> Payments { get; set; } = new();
    public int AllCount { get; set; }
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int RejectedCount { get; set; }

    [BindProperty(SupportsGet = true)]
    public PaymentStatus? StatusFilter { get; set; }

    public PaymentsModel(AdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task OnGetAsync()
    {
        var all = await _dataService.GetPaymentRequestsAsync();
        AllCount = all.Count;
        PendingCount = all.Count(p => p.Status == PaymentStatus.Pending);
        ConfirmedCount = all.Count(p => p.Status == PaymentStatus.Confirmed);
        RejectedCount = all.Count(p => p.Status == PaymentStatus.Rejected);

        Payments = StatusFilter.HasValue
            ? all.Where(p => p.Status == StatusFilter.Value).ToList()
            : all;
    }

    public async Task<IActionResult> OnPostApproveAsync(string paymentId)
    {
        string admin = User.Identity?.Name ?? "admin";
        var res = await _dataService.ApprovePaymentRequestAsync(paymentId, admin);

        if (res.Success)
            TempData["SuccessMessage"] = res.Message;
        else
            TempData["ErrorMessage"] = res.Message;

        return RedirectToPage(new { status = StatusFilter });
    }

    public async Task<IActionResult> OnPostRejectAsync(string paymentId, string reason)
    {
        string admin = User.Identity?.Name ?? "admin";
        var res = await _dataService.RejectPaymentRequestAsync(paymentId, reason, admin);

        if (res.Success)
            TempData["SuccessMessage"] = res.Message;
        else
            TempData["ErrorMessage"] = res.Message;

        return RedirectToPage(new { status = StatusFilter });
    }
}
