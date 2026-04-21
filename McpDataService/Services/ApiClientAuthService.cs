using System.Security.Cryptography;
using System.Text;

namespace McpDataService.Services;

public sealed class ApiClientCredential
{
    public string ApplicationName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class ApiClientAuthService
{
    public const string ApplicationHeaderName = "X-MCP-Application";
    public const string KeyHeaderName = "X-MCP-Key";

    private readonly AppSettingsService _appSettingsService;

    public ApiClientAuthService(AppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
    }

    public IReadOnlyList<ApiClientCredential> GetClients()
    {
        return _appSettingsService.GetApiClients();
    }

    public ApiClientCredential CreateClient(string applicationName)
    {
        var normalizedName = NormalizeApplicationName(applicationName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Application name is required.", nameof(applicationName));
        }

        var existing = _appSettingsService.GetApiClients()
            .Any(c => string.Equals(c.ApplicationName, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (existing)
        {
            throw new InvalidOperationException($"Esiste già una chiave per l'applicazione '{normalizedName}'.");
        }

        var client = new ApiClientCredential
        {
            ApplicationName = normalizedName,
            ApiKey = GenerateApiKey(),
            Enabled = true,
            CreatedUtc = DateTime.UtcNow
        };

        return _appSettingsService.UpsertApiClient(client);
    }

    public ApiClientCredential RegenerateClientKey(string applicationName)
    {
        var normalizedName = NormalizeApplicationName(applicationName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Application name is required.", nameof(applicationName));
        }

        var existing = _appSettingsService.GetApiClients()
            .FirstOrDefault(c => string.Equals(c.ApplicationName, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            throw new KeyNotFoundException($"Applicazione '{normalizedName}' non trovata.");
        }

        existing.ApiKey = GenerateApiKey();
        existing.Enabled = true;
        existing.CreatedUtc = DateTime.UtcNow;
        return _appSettingsService.UpsertApiClient(existing);
    }

    public bool DeleteClient(string applicationName)
    {
        var normalizedName = NormalizeApplicationName(applicationName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        return _appSettingsService.RemoveApiClient(normalizedName);
    }

    public bool TryValidateRequest(HttpContext context, out string applicationName)
    {
        applicationName = string.Empty;

        if (context.User.Identity?.IsAuthenticated == true
            && (context.User.IsInRole(AdminUiAuthService.AdminRole) || context.User.IsInRole("AdminUi")))
        {
            applicationName = context.User.Identity?.Name ?? "AdminUi";
            return true;
        }

        var requestAppName = context.Request.Headers[ApplicationHeaderName].FirstOrDefault()?.Trim();
        var requestKey = context.Request.Headers[KeyHeaderName].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(requestAppName) || string.IsNullOrWhiteSpace(requestKey))
        {
            return false;
        }

        var clients = _appSettingsService.GetApiClients();
        var client = clients.FirstOrDefault(c =>
            c.Enabled &&
            string.Equals(c.ApplicationName, requestAppName, StringComparison.OrdinalIgnoreCase) &&
            FixedTimeEquals(c.ApiKey, requestKey));

        if (client is null)
        {
            return false;
        }

        applicationName = client.ApplicationName;
        return true;
    }

    public string GenerateApiKey()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return $"mcp-sk-{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string NormalizeApplicationName(string applicationName)
    {
        return applicationName.Trim();
    }
}
