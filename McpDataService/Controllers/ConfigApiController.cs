using System.Net;
using McpDataService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpDataService.Controllers;

[ApiController]
[Route("api/config")]
[Authorize(Policy = "AdminOnly")]
public class ConfigApiController : ControllerBase
{
    private readonly AppSettingsService _appSettingsService;
    private readonly ApiClientAuthService _apiClientAuthService;
    private readonly AdminUiAuthService _adminUiAuthService;
    private readonly TrayIconService? _trayIconService;

    public ConfigApiController(
        AppSettingsService appSettingsService,
        ApiClientAuthService apiClientAuthService,
        AdminUiAuthService adminUiAuthService,
        TrayIconService? trayIconService = null)
    {
        _appSettingsService = appSettingsService;
        _apiClientAuthService = apiClientAuthService;
        _adminUiAuthService = adminUiAuthService;
        _trayIconService = trayIconService;
    }

    [HttpPost("apikeys/generate")]
    public IActionResult GenerateApiClientKey([FromForm] string applicationName)
    {
        try
        {
            var created = _apiClientAuthService.CreateClient(applicationName);
            return Ok(new
            {
                message = $"Chiave API creata per '{created.ApplicationName}'.",
                success = true
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, success = false });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message, success = false });
        }
    }

    [HttpPost("apikeys/regenerate")]
    public IActionResult RegenerateApiClientKey([FromForm] string applicationName)
    {
        try
        {
            var updated = _apiClientAuthService.RegenerateClientKey(applicationName);
            return Ok(new
            {
                message = $"Chiave API rigenerata per '{updated.ApplicationName}'.",
                success = true
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, success = false });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message, success = false });
        }
    }

    [HttpPost("apikeys/delete")]
    public IActionResult DeleteApiClientKey([FromForm] string applicationName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            return BadRequest(new { message = "Application name is required.", success = false });
        }

        var removed = _apiClientAuthService.DeleteClient(applicationName);
        if (!removed)
        {
            return NotFound(new { message = "Applicazione non trovata.", success = false });
        }

        return Ok(new { message = "Chiave API rimossa.", success = true });
    }

    [HttpPost("connections")]
    public IActionResult SaveConnections([FromForm] string SqlServer, [FromForm] string Oracle, [FromForm] string SQLite)
    {
        _appSettingsService.SaveConnections(SqlServer, Oracle, SQLite);
        return Ok(new { message = "Connessioni salvate con successo", success = true });
    }

    [HttpPost("current-database")]
    public IActionResult SaveCurrentDatabase([FromForm] string CurrentDatabase)
    {
        if (_trayIconService is not null)
        {
            _trayIconService.ApplyCurrentDatabase(CurrentDatabase, persistSetting: true);
        }
        else
        {
            _appSettingsService.SetCurrentDatabase(CurrentDatabase);
        }
        var selected = _appSettingsService.GetCurrentDatabase();
        return Content(
            $"<span class=\"text-success\">Database corrente impostato su {selected}.</span>",
            "text/html");
    }

    [HttpPost("tray")]
    public IActionResult SaveTrayIconSettings([FromForm] bool Enabled)
    {
        _appSettingsService.SetTrayIconEnabled(Enabled);
        _trayIconService?.ApplyEnabledState(Enabled, persistSetting: false);

        var message = Enabled
            ? "Tray icon abilitata"
            : "Tray icon disabilitata";

        return Ok(new { message, success = true, enabled = Enabled });
    }

    [HttpPost("users/upsert")]
    public IActionResult UpsertUiUser(
        [FromForm] string username,
        [FromForm] string? password,
        [FromForm] string role,
        [FromForm] string? originalUsername = null)
    {
        try
        {
            var savedUser = _adminUiAuthService.UpsertUser(username, password, role, originalUsername);
            var safeUsername = WebUtility.HtmlEncode(savedUser.Username);
            var safeRole = WebUtility.HtmlEncode(AdminUiAuthService.GetRoleLabel(savedUser.Role));
            var safeOriginalUsername = WebUtility.HtmlEncode(originalUsername?.Trim() ?? string.Empty);
            var isEdit = !string.IsNullOrWhiteSpace(originalUsername);
            var actionLabel = isEdit ? "aggiornato" : "salvato";
            var renameLabel = isEdit && !string.Equals(safeOriginalUsername, safeUsername, StringComparison.Ordinal)
                ? $" (prima: '{safeOriginalUsername}')"
                : string.Empty;
            return Content(
                $"<span class=\"text-success\">Utente '{safeUsername}' {actionLabel} con ruolo {safeRole}{renameLabel}.</span>",
                "text/html");
        }
        catch (ArgumentException ex)
        {
            return BadRequest($"<span class=\"text-danger\">{WebUtility.HtmlEncode(ex.Message)}</span>");
        }
        catch (InvalidOperationException ex)
        {
            return Conflict($"<span class=\"text-danger\">{WebUtility.HtmlEncode(ex.Message)}</span>");
        }
    }

    [HttpPost("users/delete")]
    public IActionResult DeleteUiUser([FromForm] string username)
    {
        if (_adminUiAuthService.TryRemoveUser(username, out var errorMessage))
        {
            var safeUsername = WebUtility.HtmlEncode(username.Trim());
            return Content(
                $"<span class=\"text-success\">Utente '{safeUsername}' eliminato.</span>",
                "text/html");
        }

        return BadRequest($"<span class=\"text-danger\">{WebUtility.HtmlEncode(errorMessage)}</span>");
    }

    [HttpPost("shutdown")]
    public IActionResult Shutdown()
    {
        _trayIconService?.ShowBalloon("MCP Data Service", "Servizio in chiusura...");

        Task.Delay(1000).ContinueWith(_ =>
        {
            Environment.Exit(0);
        });

        return Ok(new { message = "Servizio in chiusura...", success = true });
    }
}
