using McpDataService.Services;

namespace McpDataService.Middleware;

public class ApiClientAuthMiddleware
{
    public const string ApiClientApplicationContextKey = "ApiClientApplication";

    private readonly RequestDelegate _next;

    public ApiClientAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApiClientAuthService authService)
    {
        if (!RequiresSiteAuth(context.Request))
        {
            await _next(context);
            return;
        }

        if (!IsApiRequest(context.Request.Path) && context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        if (authService.TryValidateRequest(context, out var applicationName))
        {
            if (context.Request.Headers.ContainsKey(ApiClientAuthService.ApplicationHeaderName))
            {
                context.Items[ApiClientApplicationContextKey] = applicationName;
            }

            await _next(context);
            return;
        }

        if (IsApiRequest(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = "Unauthorized",
                message =
                    $"Specificare header validi '{ApiClientAuthService.ApplicationHeaderName}' e '{ApiClientAuthService.KeyHeaderName}' oppure autenticarsi come amministratore."
            });
            return;
        }

        var returnUrl = Uri.EscapeDataString($"{context.Request.Path}{context.Request.QueryString}");
        context.Response.Redirect($"/Login?returnUrl={returnUrl}");
    }

    private static bool RequiresSiteAuth(HttpRequest request)
    {
        if (HttpMethods.IsOptions(request.Method))
        {
            return false;
        }

        var path = request.Path;
        if (!path.HasValue)
        {
            return false;
        }

        if (path.StartsWithSegments("/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/setupadmin", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/logout", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsApiRequest(PathString path)
    {
        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/db", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/crud", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase);
    }
}
