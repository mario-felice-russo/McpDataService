using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using McpDataService.Data;
using McpDataService.Models.Audit;
using McpDataService.Services;
using Serilog;

namespace McpDataService.Middleware;

public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceProvider _serviceProvider;

    public AuditMiddleware(RequestDelegate next, IServiceProvider serviceProvider)
    {
        _next = next;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;

        if (ShouldSkipAudit(request.Path))
        {
            await _next(context);
            return;
        }

        var requestBody = await ReadRequestBodyAsync(request);
        var filteredHeaders = GetFilteredHeaders(request);

        var (userType, userIdentifier, dbType) = GetRequestContext(context, requestBody);

        var auditRequest = BuildAuditRequest(request, filteredHeaders, requestBody, userType, userIdentifier, dbType, context);

        AddRouteParameters(request, auditRequest);

        var originalResponseBodyStream = context.Response.Body;
        using (var responseBodyStream = new MemoryStream())
        {
            context.Response.Body = responseBodyStream;
            await _next(context);

            responseBodyStream.Position = 0;
            var responseBody = await new StreamReader(responseBodyStream, Encoding.UTF8).ReadToEndAsync();
            responseBodyStream.Position = 0;
            await responseBodyStream.CopyToAsync(originalResponseBodyStream);

            auditRequest.ResponseStatus = context.Response.StatusCode;
            auditRequest.ResponseBody = responseBody;
            auditRequest.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
                    auditDb.Requests.Add(auditRequest);
                    await auditDb.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "AuditMiddleware: failed to save audit request");
                }
            });
        }
    }

    private static bool ShouldSkipAudit(PathString path)
    {
        return path.StartsWithSegments("/api/audit") || path.StartsWithSegments("/audit");
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        var requestBodyStream = new MemoryStream();
        await request.Body.CopyToAsync(requestBodyStream);
        request.Body.Position = 0;
        requestBodyStream.Position = 0;
        var requestBody = await new StreamReader(requestBodyStream, Encoding.UTF8).ReadToEndAsync();
        request.Body.Position = 0;
        return requestBody;
    }

    private static string GetFilteredHeaders(HttpRequest request)
    {
        var headers = request.Headers
            .Where(h => !IsSensitiveHeader(h.Key))
            .ToDictionary(h => h.Key, h => h.Value.ToString());
        return JsonSerializer.Serialize(headers);
    }

    private static AuditRequest BuildAuditRequest(HttpRequest request, string filteredHeaders, string requestBody, string userType, string userIdentifier, string dbType, HttpContext context)
    {
        return new AuditRequest
        {
            Endpoint = request.Path,
            HttpMethod = request.Method,
            RequestHeaders = filteredHeaders,
            RequestBody = requestBody,
            UserType = userType,
            UserIdentifier = userIdentifier,
            DbType = dbType,
            IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            IsMcpRequest = request.Path.StartsWithSegments("/mcp")
        };
    }

    private static void AddRouteParameters(HttpRequest request, AuditRequest auditRequest)
    {
        if (request.RouteValues.TryGetValue("dbType", out var dbTypeValue))
        {
            auditRequest.Parameters.Add(new AuditRequestParameter
            {
                Key = "db_type",
                Value = dbTypeValue?.ToString() ?? ""
            });
        }

        if (request.RouteValues.TryGetValue("table", out var tableValue))
        {
            auditRequest.Parameters.Add(new AuditRequestParameter
            {
                Key = "table",
                Value = tableValue?.ToString() ?? ""
            });
        }
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        return headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
               || headerName.Equals(ApiClientAuthService.ApplicationHeaderName, StringComparison.OrdinalIgnoreCase)
               || headerName.Equals(ApiClientAuthService.KeyHeaderName, StringComparison.OrdinalIgnoreCase)
               || headerName.Equals("X-DB-Admin-Key", StringComparison.OrdinalIgnoreCase)
               || headerName.Equals("X-Service-Key", StringComparison.OrdinalIgnoreCase);
    }

    private static (string userType, string userIdentifier, string dbType) GetRequestContext(HttpContext context, string requestBody)
    {
        var userType = "Anonymous";
        var userIdentifier = "Unknown";
        var dbType = context.Request.RouteValues["dbType"]?.ToString() ?? "None";
        var hasApiClientApplication = TryGetApiClientApplication(context, out var nomeApplicazione);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var role = ResolveUiRole(context.User);
            if (role is null)
            {
                role = AdminUiAuthService.UserRole;
            }

            userType = role switch
            {
                AdminUiAuthService.AdminRole => "AdminUi",
                AdminUiAuthService.ProgrammerRole => "ProgrammatoreUi",
                _ => "UserUi"
            };
            userIdentifier = hasApiClientApplication ? nomeApplicazione : context.User.Identity?.Name ?? "AdminUi";

            if (context.Request.Path.StartsWithSegments("/mcp") && dbType == "None")
            {
                dbType = ResolveMcpDbType(requestBody);
            }
            return (userType, userIdentifier, dbType);
        }

        if (hasApiClientApplication)
        {
            userType = context.Request.Path.StartsWithSegments("/mcp")
                ? "MCPClient"
                : "ApiClient";
            userIdentifier = nomeApplicazione;
        }
        else if (context.Request.Path.StartsWithSegments("/mcp"))
        {
            userType = "MCPClient";
        }
        else if (context.Request.Path.StartsWithSegments("/db")
                 || context.Request.Path.StartsWithSegments("/crud")
                 || context.Request.Path.StartsWithSegments("/api"))
        {
            userType = "ApiClient";
        }

        if (context.Request.Path.StartsWithSegments("/mcp") && dbType == "None")
        {
            dbType = ResolveMcpDbType(requestBody);
        }

        return (userType, userIdentifier, dbType);
    }

    private static string? ResolveUiRole(ClaimsPrincipal user)
    {
        if (user.IsInRole(AdminUiAuthService.AdminRole) || user.IsInRole("AdminUi"))
        {
            return AdminUiAuthService.AdminRole;
        }

        if (user.IsInRole(AdminUiAuthService.ProgrammerRole))
        {
            return AdminUiAuthService.ProgrammerRole;
        }

        if (user.IsInRole(AdminUiAuthService.UserRole))
        {
            return AdminUiAuthService.UserRole;
        }

        return null;
    }

    private static bool TryGetApiClientApplication(HttpContext context, out string applicationName)
    {
        if (context.Items.TryGetValue(ApiClientAuthMiddleware.ApiClientApplicationContextKey, out var value)
            && value is string itemApplication
            && !string.IsNullOrWhiteSpace(itemApplication))
        {
            applicationName = itemApplication;
            return true;
        }

        applicationName = context.Request.Headers[ApiClientAuthService.ApplicationHeaderName].FirstOrDefault()?.Trim() ?? "";
        return !string.IsNullOrWhiteSpace(applicationName);
    }

    private static string ResolveMcpDbType(string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return "None";
        }

        try
        {
            using var document = JsonDocument.Parse(requestBody);
            
            var root = document.RootElement;

            if (root.TryGetProperty("params", out var parameters)
                && parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("db_type", out var dbTypeElement)
            )
            {
                return dbTypeElement.GetString() ?? "None";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AuditMiddleware: failed to parse MCP db type");
        }

        return "None";
    }
}
