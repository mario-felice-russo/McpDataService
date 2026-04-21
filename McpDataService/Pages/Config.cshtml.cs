using McpDataService.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McpDataService.Pages;

public class ConfigModel : PageModel
{
    private static readonly IReadOnlyList<string> AvailableDatabaseOptions = ["SqlServer", "Oracle", "SQLite"];

    private readonly IConfiguration _configuration;
    private readonly AppSettingsService _appSettingsService;
    private readonly ApiClientAuthService _apiClientAuthService;
    private readonly AdminUiAuthService _adminUiAuthService;

    public ConfigModel(
        IConfiguration configuration,
        AppSettingsService appSettingsService,
        ApiClientAuthService apiClientAuthService,
        AdminUiAuthService adminUiAuthService)
    {
        _configuration = configuration;
        _appSettingsService = appSettingsService;
        _apiClientAuthService = apiClientAuthService;
        _adminUiAuthService = adminUiAuthService;
    }

    public IReadOnlyList<UiUserCredential> UiUsers { get; set; } = Array.Empty<UiUserCredential>();
    public IReadOnlyList<ApiClientCredential> ApiClients { get; set; } = Array.Empty<ApiClientCredential>();
    public string SqlServerConn { get; set; } = "";
    public string OracleConn { get; set; } = "";
    public string SQLiteConn { get; set; } = "";
    public bool TrayIconEnabled { get; set; }
    public string CurrentDatabase { get; set; } = "SqlServer";
    public IReadOnlyList<string> SupportedDatabases => AvailableDatabaseOptions;
    public IReadOnlyList<string> SupportedRoles => AdminUiAuthService.SupportedRoles;
    public string BaseUrl { get; set; } = "";
    public string ApplicationHeaderName => ApiClientAuthService.ApplicationHeaderName;
    public string KeyHeaderName => ApiClientAuthService.KeyHeaderName;
    public string CurrentUsername { get; set; } = string.Empty;

    public void OnGet()
    {
        UiUsers = _adminUiAuthService.GetUsers();
        ApiClients = _apiClientAuthService.GetClients();
        SqlServerConn = _configuration.GetConnectionString("SqlServer") ?? "";
        OracleConn = _configuration.GetConnectionString("Oracle") ?? "";
        SQLiteConn = _configuration.GetConnectionString("SQLite") ?? "";
        TrayIconEnabled = _configuration.GetValue<bool?>("TrayIcon:Enabled") ?? true;
        CurrentDatabase = _appSettingsService.GetCurrentDatabase();
        CurrentUsername = User.Identity?.Name?.Trim() ?? string.Empty;

        var urls = _configuration["Urls"] ?? "http://localhost:5000";
        BaseUrl = urls;
    }
}
