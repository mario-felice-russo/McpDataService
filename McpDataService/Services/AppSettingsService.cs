using System.Text.Json;
using System.Text.Json.Nodes;

namespace McpDataService.Services;

public class AppSettingsService
{
    private static readonly string[] SupportedDatabases = ["SqlServer", "Oracle", "SQLite"];
    private readonly string _appSettingsPath;
    private readonly object _syncRoot = new();

    public AppSettingsService(IHostEnvironment environment)
    {
        _appSettingsPath = Path.Combine(environment.ContentRootPath, "appsettings.json");
    }

    public IReadOnlyList<ApiClientCredential> GetApiClients()
    {
        lock (_syncRoot)
        {
            var root = ReadRoot();
            return ReadApiClients(root)
                .OrderBy(c => c.ApplicationName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public ApiClientCredential UpsertApiClient(ApiClientCredential client)
    {
        lock (_syncRoot)
        {
            var root = ReadRoot();
            var clients = ReadApiClients(root).ToList();
            var index = clients.FindIndex(c =>
                string.Equals(c.ApplicationName, client.ApplicationName, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                clients[index] = client;
            }
            else
            {
                clients.Add(client);
            }

            WriteApiClients(root, clients);
            WriteRoot(root);
            return client;
        }
    }

    public bool RemoveApiClient(string applicationName)
    {
        lock (_syncRoot)
        {
            var root = ReadRoot();
            var clients = ReadApiClients(root).ToList();
            var removed = clients.RemoveAll(c =>
                string.Equals(c.ApplicationName, applicationName, StringComparison.OrdinalIgnoreCase));

            if (removed == 0)
            {
                return false;
            }

            WriteApiClients(root, clients);
            WriteRoot(root);
            return true;
        }
    }

    public IReadOnlyList<UiUserCredential> GetUiUsers()
    {
        lock (_syncRoot)
        {
            var root = ReadRoot();
            return ReadUiUsers(root)
                .OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public UiUserCredential UpsertUiUser(UiUserCredential user)
    {
        lock (_syncRoot)
        {
            var root = ReadRoot();
            var users = ReadUiUsers(root).ToList();
            var index = users.FindIndex(existing =>
                string.Equals(existing.Username, user.Username, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                user.CreatedUtc = users[index].CreatedUtc;
                users[index] = user;
            }
            else
            {
                users.Add(user);
            }

            WriteUiUsers(root, users);
            WriteRoot(root);
            return user;
        }
    }

    public bool RemoveUiUser(string username)
    {
        lock (_syncRoot)
        {
            var root = ReadRoot();
            var users = ReadUiUsers(root).ToList();
            var removed = users.RemoveAll(user =>
                string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase));

            if (removed == 0)
            {
                return false;
            }

            WriteUiUsers(root, users);
            WriteRoot(root);
            return true;
        }
    }

    public void SaveConnections(string sqlServer, string oracle, string sqlite)
    {
        Update(root =>
        {
            var connectionStrings = EnsureObject(root, "ConnectionStrings");
            connectionStrings["SqlServer"] = sqlServer;
            connectionStrings["Oracle"] = oracle;
            connectionStrings["SQLite"] = sqlite;

            if (connectionStrings["AuditDb"] is null)
            {
                connectionStrings["AuditDb"] = "Data Source=audit.db";
            }
        });
    }

    public void SetTrayIconEnabled(bool enabled)
    {
        Update(root =>
        {
            var trayIcon = EnsureObject(root, "TrayIcon");
            trayIcon["Enabled"] = enabled;
        });
    }

    public void SetAdminUiCredentials(string username, string passwordHash)
    {
        UpsertUiUser(new UiUserCredential
        {
            Username = string.IsNullOrWhiteSpace(username) ? "admin" : username.Trim(),
            PasswordHash = passwordHash?.Trim() ?? string.Empty,
            Role = AdminUiAuthService.AdminRole,
            CreatedUtc = DateTime.UtcNow
        });
    }

    public string GetCurrentDatabase()
    {
        lock (_syncRoot)
        {
            var root = ReadRoot();
            var configuredDb = root["CurrentDatabase"]?.GetValue<string>();
            return NormalizeDatabase(configuredDb);
        }
    }

    public void SetCurrentDatabase(string database)
    {
        var normalized = NormalizeDatabase(database);
        Update(root =>
        {
            root["CurrentDatabase"] = normalized;
        });
    }

    private void Update(Action<JsonObject> apply)
    {
        lock (_syncRoot)
        {
            var root = ReadRoot();
            apply(root);
            WriteRoot(root);
        }
    }

    private JsonObject ReadRoot()
    {
        if (!File.Exists(_appSettingsPath))
        {
            return new JsonObject();
        }

        var json = File.ReadAllText(_appSettingsPath);
        return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
    }

    private static JsonObject EnsureObject(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        root[propertyName] = created;
        return created;
    }

    private static IReadOnlyList<ApiClientCredential> ReadApiClients(JsonObject root)
    {
        if (root["ApiAuth"] is not JsonObject apiAuth || apiAuth["Clients"] is not JsonArray clientsNode)
        {
            return Array.Empty<ApiClientCredential>();
        }

        var clients = clientsNode.Deserialize<List<ApiClientCredential>>() ?? new List<ApiClientCredential>();
        return clients
            .Where(c => !string.IsNullOrWhiteSpace(c.ApplicationName) && !string.IsNullOrWhiteSpace(c.ApiKey))
            .Select(c => new ApiClientCredential
            {
                ApplicationName = c.ApplicationName.Trim(),
                ApiKey = c.ApiKey.Trim(),
                Enabled = c.Enabled,
                CreatedUtc = c.CreatedUtc == default ? DateTime.UtcNow : c.CreatedUtc
            })
            .ToList();
    }

    private static IReadOnlyList<UiUserCredential> ReadUiUsers(JsonObject root)
    {
        if (root["UserAuth"] is JsonObject userAuth && userAuth["Users"] is JsonArray usersNode)
        {
            var users = usersNode.Deserialize<List<UiUserCredential>>() ?? new List<UiUserCredential>();
            return users
                .Where(user => !string.IsNullOrWhiteSpace(user.Username) && !string.IsNullOrWhiteSpace(user.PasswordHash))
                .Select(SanitizeUiUser)
                .ToList();
        }

        if (root["AdminUiAuth"] is JsonObject legacyAuth)
        {
            var username = legacyAuth["Username"]?.GetValue<string>();
            var passwordHash = legacyAuth["PasswordHash"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(passwordHash))
            {
                return
                [
                    new UiUserCredential
                    {
                        Username = username.Trim(),
                        PasswordHash = passwordHash.Trim(),
                        Role = AdminUiAuthService.AdminRole,
                        CreatedUtc = DateTime.UtcNow
                    }
                ];
            }
        }

        return Array.Empty<UiUserCredential>();
    }

    private static void WriteApiClients(JsonObject root, IEnumerable<ApiClientCredential> clients)
    {
        var apiAuth = EnsureObject(root, "ApiAuth");
        var headers = EnsureObject(apiAuth, "Headers");
        headers["Application"] = ApiClientAuthService.ApplicationHeaderName;
        headers["Key"] = ApiClientAuthService.KeyHeaderName;

        var cleanClients = clients
            .Where(c => !string.IsNullOrWhiteSpace(c.ApplicationName) && !string.IsNullOrWhiteSpace(c.ApiKey))
            .Select(c => new ApiClientCredential
            {
                ApplicationName = c.ApplicationName.Trim(),
                ApiKey = c.ApiKey.Trim(),
                Enabled = c.Enabled,
                CreatedUtc = c.CreatedUtc == default ? DateTime.UtcNow : c.CreatedUtc
            })
            .GroupBy(c => c.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .OrderBy(c => c.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        apiAuth["Clients"] = JsonSerializer.SerializeToNode(cleanClients) ?? new JsonArray();
    }

    private static void WriteUiUsers(JsonObject root, IEnumerable<UiUserCredential> users)
    {
        var userAuth = EnsureObject(root, "UserAuth");

        var cleanUsers = users
            .Where(user => !string.IsNullOrWhiteSpace(user.Username) && !string.IsNullOrWhiteSpace(user.PasswordHash))
            .Select(SanitizeUiUser)
            .GroupBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();

        userAuth["Users"] = JsonSerializer.SerializeToNode(cleanUsers) ?? new JsonArray();
        root.Remove("AdminUiAuth");
    }

    private static UiUserCredential SanitizeUiUser(UiUserCredential user)
    {
        var createdUtc = user.CreatedUtc == default
            ? DateTime.UtcNow
            : user.CreatedUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(user.CreatedUtc, DateTimeKind.Utc)
                : user.CreatedUtc.ToUniversalTime();

        return new UiUserCredential
        {
            Username = user.Username.Trim(),
            PasswordHash = user.PasswordHash.Trim(),
            Role = AdminUiAuthService.NormalizeRole(user.Role),
            CreatedUtc = createdUtc
        };
    }

    private void WriteRoot(JsonObject root)
    {
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_appSettingsPath, json);
    }

    private static string NormalizeDatabase(string? database)
    {
        if (string.IsNullOrWhiteSpace(database))
        {
            return SupportedDatabases[0];
        }

        var match = SupportedDatabases
            .FirstOrDefault(db => string.Equals(db, database.Trim(), StringComparison.OrdinalIgnoreCase));

        return match ?? SupportedDatabases[0];
    }
}
