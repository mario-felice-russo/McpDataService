using System.Security.Claims;
using McpDataService.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McpDataService.Pages;

public class LoginModel : PageModel
{
    private readonly AdminUiAuthService _adminUiAuthService;

    public LoginModel(AdminUiAuthService adminUiAuthService)
    {
        _adminUiAuthService = adminUiAuthService;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(ReturnUrl);
        }

        if (!_adminUiAuthService.IsSetupComplete())
        {
            return RedirectToPage("/SetupAdmin", new { returnUrl = ReturnUrl });
        }

        Username = _adminUiAuthService.GetConfiguredUsername();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(ReturnUrl);
        }

        if (!_adminUiAuthService.IsSetupComplete())
        {
            return RedirectToPage("/SetupAdmin", new { returnUrl = ReturnUrl });
        }

        if (!_adminUiAuthService.ValidateCredentials(Username, Password, out var authenticatedUser) || authenticatedUser is null)
        {
            ErrorMessage = "Credenziali non valide.";
            Username = _adminUiAuthService.GetConfiguredUsername();
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, authenticatedUser.Username),
            new(ClaimTypes.Role, authenticatedUser.Role)
        };

        if (string.Equals(authenticatedUser.Role, AdminUiAuthService.AdminRole, StringComparison.Ordinal))
        {
            claims.Add(new Claim(ClaimTypes.Role, "AdminUi"));
        }

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(45)
            });

        return RedirectToLocal(ReturnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Index");
    }
}
