using KitoGame.AdminWeb.Services;
using KitoGame.Shared.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KitoGame.AdminWeb.Pages;

public class AuditModel : PageModel
{
    private readonly AdminDataService _dataService;

    public List<AuditLog> Logs { get; set; } = new();

    public AuditModel(AdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task OnGetAsync()
    {
        Logs = await _dataService.GetAuditLogsAsync(100);
    }
}

