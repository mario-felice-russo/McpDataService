using System.Security.Claims;
using McpDataService.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McpDataService.Pages;

public class SetupAdminModel : PageModel
{
    private readonly AdminUiAuthService _adminUiAuthService;

    public SetupAdminModel(AdminUiAuthService adminUiAuthService)
    {
        _adminUiAuthService = adminUiAuthService;
    }

    [BindProperty]
    public string Username { get; set; } = "admin";

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (_adminUiAuthService.IsSetupComplete())
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(ReturnUrl);
            }

            return RedirectToPage("/Login", new { returnUrl = ReturnUrl });
        }

        Username = _adminUiAuthService.GetConfiguredUsername();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_adminUiAuthService.IsSetupComplete())
        {
            return RedirectToPage("/Login", new { returnUrl = ReturnUrl });
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Lo username è obbligatorio.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 10)
        {
            ErrorMessage = "La password deve contenere almeno 10 caratteri.";
            return Page();
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Le password non coincidono.";
            return Page();
        }

        _adminUiAuthService.SetCredentials(Username, Password);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, Username.Trim()),
            new(ClaimTypes.Role, AdminUiAuthService.AdminRole),
            new(ClaimTypes.Role, "AdminUi")
        };

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
