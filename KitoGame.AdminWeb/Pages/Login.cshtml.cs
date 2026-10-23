using KitoGame.AdminWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace KitoGame.AdminWeb.Pages;

public class LoginModel : PageModel
{
    private readonly AdminDataService _dataService;

    public LoginModel(AdminDataService dataService)
    {
        _dataService = dataService;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Por favor ingresa tu usuario y contraseña.";
            return Page();
        }

        string cleanUser = Username.Trim().ToLowerInvariant();

        // Validar si es una cuenta administradora autorizada
        bool isMasterUsername = cleanUser == "ivan" || cleanUser == "ivan@kitogame.com" || cleanUser == "ivan.pautt@gmail.com";

        // Consultar usuario en la base de datos de MongoDB
        var users = await _dataService.GetUsersAsync();
        var adminUser = users.FirstOrDefault(u =>
            (u.Username.Equals(cleanUser, StringComparison.OrdinalIgnoreCase) ||
             u.Email.Equals(cleanUser, StringComparison.OrdinalIgnoreCase)) &&
            (u.IsAdmin || isMasterUsername));

        bool passwordMatches = false;

        if (adminUser != null)
        {
            passwordMatches = adminUser.Password == Password;
        }

        // Respaldo de acceso maestro para el operador administrativo de KitoGame
        if (!passwordMatches && isMasterUsername && Password == "1234")
        {
            passwordMatches = true;
        }

        if (!passwordMatches)
        {
            ErrorMessage = "Credenciales incorrectas o el usuario no tiene permisos de administrador.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, adminUser?.DisplayName ?? "Ivan Pautt (Admin)"),
            new Claim(ClaimTypes.NameIdentifier, adminUser?.Id ?? "admin-master"),
            new Claim(ClaimTypes.Role, "SuperAdmin"),
            new Claim("Username", cleanUser)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        await _dataService.LogAuditAsync(cleanUser, "AdminLogin", "Backoffice", "Inicio de sesión exitoso al panel de control maestro.");

        return RedirectToPage("/Index");
    }
}

