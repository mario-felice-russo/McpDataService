using System.Security.Cryptography;

namespace McpDataService.Services;
public sealed class UiUserCredential
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = AdminUiAuthService.UserRole;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AuthenticatedUiUser
{
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = AdminUiAuthService.UserRole;
}

public class AdminUiAuthService
{
    private const int Iterations = 120_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const string Prefix = "pbkdf2-sha256";
    public const string AdminRole = "admin";
    public const string UserRole = "user";
    public const string ProgrammerRole = "programmatore";
    private static readonly string[] AllowedRoles = { AdminRole, UserRole, ProgrammerRole };

    private readonly AppSettingsService _appSettingsService;

    public AdminUiAuthService(AppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
    }

    public static IReadOnlyList<string> SupportedRoles => AllowedRoles;

    public bool IsSetupComplete()
    {
        return _appSettingsService.GetUiUsers().Count > 0;
    }

    public string GetConfiguredUsername()
    {
        var users = _appSettingsService.GetUiUsers();
        var preferred = users
            .FirstOrDefault(user => IsAdminRole(user.Role))
            ?? users.FirstOrDefault();

        return preferred?.Username ?? "admin";
    }

    public IReadOnlyList<UiUserCredential> GetUsers()
    {
        return _appSettingsService.GetUiUsers();
    }

    public bool ValidateCredentials(string username, string password, out AuthenticatedUiUser? authenticatedUser)
    {
        authenticatedUser = null;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var normalizedUsername = username.Trim();
        var configuredUser = _appSettingsService.GetUiUsers()
            .FirstOrDefault(user => string.Equals(user.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase));

        if (configuredUser is null || string.IsNullOrWhiteSpace(configuredUser.PasswordHash))
        {
            return false;
        }

        if (!VerifyPassword(password, configuredUser.PasswordHash))
        {
            return false;
        }

        authenticatedUser = new AuthenticatedUiUser
        {
            Username = configuredUser.Username,
            Role = NormalizeRole(configuredUser.Role)
        };

        return true;
    }

    public void SetCredentials(string username, string password)
    {
        UpsertUser(username, password, AdminRole);
    }

    public UiUserCredential UpsertUser(string username, string? password, string role, string? originalUsername = null)
    {
        var safeUsername = username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(safeUsername))
        {
            throw new ArgumentException("Lo username è obbligatorio.", nameof(username));
        }

        if (!TryNormalizeRole(role, out var normalizedRole))
        {
            throw new ArgumentException($"Ruolo non valido: {role}.", nameof(role));
        }

        var users = _appSettingsService.GetUiUsers();
        UiUserCredential? userBeingEdited;
        if (!string.IsNullOrWhiteSpace(originalUsername))
        {
            var safeOriginalUsername = originalUsername.Trim();
            userBeingEdited = users.FirstOrDefault(user =>
                string.Equals(user.Username, safeOriginalUsername, StringComparison.OrdinalIgnoreCase));
            if (userBeingEdited is null)
            {
                throw new ArgumentException($"Utente '{safeOriginalUsername}' non trovato.", nameof(originalUsername));
            }
        }
        else
        {
            userBeingEdited = users.FirstOrDefault(user =>
                string.Equals(user.Username, safeUsername, StringComparison.OrdinalIgnoreCase));
        }

        var usernameCollision = users.FirstOrDefault(user =>
            string.Equals(user.Username, safeUsername, StringComparison.OrdinalIgnoreCase));
        if (usernameCollision is not null
            && (userBeingEdited is null
                || !string.Equals(usernameCollision.Username, userBeingEdited.Username, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Esiste già un utente con username '{safeUsername}'.");
        }

        if (userBeingEdited is not null && IsAdminRole(userBeingEdited.Role) && !IsAdminRole(normalizedRole))
        {
            var hasAnotherAdmin = users.Any(user =>
                !string.Equals(user.Username, userBeingEdited.Username, StringComparison.OrdinalIgnoreCase)
                && IsAdminRole(user.Role));
            if (!hasAnotherAdmin)
            {
                throw new InvalidOperationException("Impossibile rimuovere il ruolo admin all'ultimo utente admin.");
            }
        }

        var passwordToPersist = password?.Trim() ?? string.Empty;
        string passwordHash;
        if (passwordToPersist.Length == 0)
        {
            if (userBeingEdited is null)
            {
                throw new ArgumentException("La password è obbligatoria per un nuovo utente.", nameof(password));
            }

            passwordHash = userBeingEdited.PasswordHash;
        }
        else
        {
            if (passwordToPersist.Length < 10)
            {
                throw new ArgumentException("La password deve contenere almeno 10 caratteri.", nameof(password));
            }

            passwordHash = HashPassword(passwordToPersist);
        }

        if (userBeingEdited is not null
            && !string.Equals(userBeingEdited.Username, safeUsername, StringComparison.OrdinalIgnoreCase))
        {
            _appSettingsService.RemoveUiUser(userBeingEdited.Username);
        }

        var userToSave = new UiUserCredential
        {
            Username = safeUsername,
            PasswordHash = passwordHash,
            Role = normalizedRole,
            CreatedUtc = userBeingEdited?.CreatedUtc ?? DateTime.UtcNow
        };

        return _appSettingsService.UpsertUiUser(userToSave);
    }

    public bool TryRemoveUser(string username, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            errorMessage = "Lo username è obbligatorio.";
            return false;
        }

        var normalizedUsername = username.Trim();
        var users = _appSettingsService.GetUiUsers();
        var existing = users.FirstOrDefault(user =>
            string.Equals(user.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            errorMessage = $"Utente '{normalizedUsername}' non trovato.";
            return false;
        }

        if (IsAdminRole(existing.Role))
        {
            var adminCount = users.Count(user => IsAdminRole(user.Role));
            if (adminCount <= 1)
            {
                errorMessage = "Impossibile eliminare l'ultimo utente admin.";
                return false;
            }
        }

        var removed = _appSettingsService.RemoveUiUser(existing.Username);
        if (!removed)
        {
            errorMessage = $"Utente '{normalizedUsername}' non trovato.";
            return false;
        }

        return true;
    }

    public static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return UserRole;
        }

        if (string.Equals(role, "AdminUi", StringComparison.OrdinalIgnoreCase))
        {
            return AdminRole;
        }
        if (string.Equals(role, "programmer", StringComparison.OrdinalIgnoreCase))
        {
            return ProgrammerRole;
        }

        var normalized = role.Trim().ToLowerInvariant();
        return AllowedRoles.Any(allowed => string.Equals(allowed, normalized, StringComparison.Ordinal))
            ? normalized
            : UserRole;
    }

    private static bool TryNormalizeRole(string? role, out string normalizedRole)
    {
        normalizedRole = string.Empty;
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        if (string.Equals(role, "AdminUi", StringComparison.OrdinalIgnoreCase))
        {
            normalizedRole = AdminRole;
            return true;
        }

        if (string.Equals(role, "programmer", StringComparison.OrdinalIgnoreCase))
        {
            normalizedRole = ProgrammerRole;
            return true;
        }

        var candidate = role.Trim().ToLowerInvariant();
        if (AllowedRoles.Any(allowed => string.Equals(allowed, candidate, StringComparison.Ordinal)))
        {
            normalizedRole = candidate;
            return true;
        }

        return false;
    }

    public static string GetRoleLabel(string role)
    {
        return NormalizeRole(role) switch
        {
            AdminRole => "Admin",
            ProgrammerRole => "Programmatore",
            _ => "User"
        };
    }

    public static string GetRoleIconPath(string role)
    {
        return NormalizeRole(role) switch
        {
            AdminRole => "/assets/role-admin.svg",
            ProgrammerRole => "/assets/role-programmatore.svg",
            _ => "/assets/role-user.svg"
        };
    }

    private static bool IsAdminRole(string role)
    {
        return string.Equals(NormalizeRole(role), AdminRole, StringComparison.Ordinal);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashLength);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string encoded)
    {
        var parts = encoded.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
