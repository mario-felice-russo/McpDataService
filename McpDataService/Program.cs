using McpDataService.Data;
using McpDataService.Middleware;
using McpDataService.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.File(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "app-.log"),
        rollingInterval: RollingInterval.Day,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizePage("/Config", "AdminOnly");
    options.Conventions.AuthorizePage("/Logs", "TechnicalOrAdmin");
    options.Conventions.AuthorizePage("/AuditLogs", "TechnicalOrAdmin");
    options.Conventions.AuthorizePage("/AuditLogDetail", "TechnicalOrAdmin");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/SetupAdmin");
    options.Conventions.AllowAnonymousToPage("/Logout");
});

builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AuditDb") ?? "Data Source=audit.db"));

builder.Services.AddDbContext<SqlServerContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));
builder.Services.AddDbContext<OracleContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("Oracle")));
builder.Services.AddDbContext<SQLiteContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SQLite") ?? "Data Source=app.db"));

builder.Services.AddScoped<DbIntrospectionService>();
builder.Services.AddScoped<CrudService>();
builder.Services.AddSingleton<AppSettingsService>();
builder.Services.AddSingleton<ApiClientAuthService>();
builder.Services.AddSingleton<AdminUiAuthService>();
builder.Services.AddSingleton<TrayIconService>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.Cookie.Name = "McpDataService.AdminUi";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(45);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                var authService = context.HttpContext.RequestServices.GetRequiredService<AdminUiAuthService>();
                if (!authService.IsSetupComplete() && !context.Request.Path.StartsWithSegments("/SetupAdmin"))
                {
                    var returnUrl = Uri.EscapeDataString($"{context.Request.Path}{context.Request.QueryString}");
                    context.Response.Redirect($"/SetupAdmin?returnUrl={returnUrl}");
                }
                else
                {
                    context.Response.Redirect(context.RedirectUri);
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(AdminUiAuthService.AdminRole)
            || context.User.IsInRole("AdminUi"));
    });

    options.AddPolicy("TechnicalOrAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.IsInRole(AdminUiAuthService.AdminRole)
            || context.User.IsInRole("AdminUi")
            || context.User.IsInRole(AdminUiAuthService.ProgrammerRole));
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    auditDb.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseStaticFiles();
var assetsPath = Path.Combine(app.Environment.ContentRootPath, "assets");
if (Directory.Exists(assetsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(assetsPath),
        RequestPath = "/assets"
    });
}
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<AuditMiddleware>();
app.UseMiddleware<ApiClientAuthMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

var trayIcon = app.Services.GetRequiredService<TrayIconService>();
var trayEnabled = app.Configuration.GetValue<bool?>("TrayIcon:Enabled") ?? true;
if (trayEnabled)
{
    try
    {
        trayIcon.Start();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Unable to start tray icon service.");
    }
}
else
{
    Log.Information("Tray icon disabled by configuration (TrayIcon:Enabled=false).");
}

app.Lifetime.ApplicationStopping.Register(trayIcon.Dispose);

app.Run();