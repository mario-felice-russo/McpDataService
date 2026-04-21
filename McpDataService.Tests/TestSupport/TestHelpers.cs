using System.Text.Json;
using McpDataService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace McpDataService.Tests.TestSupport;

internal static class TestHelpers
{
    public static IConfiguration BuildConfiguration(params (string Key, string? Value)[] values)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            map[key] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(map)
            .Build();
    }

    public static JsonElement ToJsonElement(object? value)
    {
        using var jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return jsonDocument.RootElement.Clone();
    }
}

internal sealed class AppSettingsTestContext : IDisposable
{
    public string RootPath { get; }
    public AppSettingsService AppSettingsService { get; }
    public ApiClientAuthService ApiClientAuthService { get; }
    public AdminUiAuthService AdminUiAuthService { get; }

    public AppSettingsTestContext()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "McpDataService.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);

        File.WriteAllText(Path.Combine(RootPath, "appsettings.json"), """
{
  "ConnectionStrings": {
    "SqlServer": "Server=(localdb)\\MSSQLLocalDB;Database=Dummy;",
    "Oracle": "Data Source=DummyOracle",
    "SQLite": "Data Source=dummy.db",
    "AuditDb": "Data Source=audit.db"
  },
  "CurrentDatabase": "SQLite",
  "ApiAuth": {
    "Headers": {
      "Application": "X-MCP-Application",
      "Key": "X-MCP-Key"
    },
    "Clients": []
  },
  "UserAuth": {
    "Users": []
  },
  "TrayIcon": {
    "Enabled": false
  }
}
""");

        var hostEnvironment = new TestHostEnvironment
        {
            ContentRootPath = RootPath,
            ContentRootFileProvider = new PhysicalFileProvider(RootPath)
        };

        AppSettingsService = new AppSettingsService(hostEnvironment);
        ApiClientAuthService = new ApiClientAuthService(AppSettingsService);
        AdminUiAuthService = new AdminUiAuthService(AppSettingsService);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for temporary test directory.
        }
    }
}

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "McpDataService.Tests";
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
