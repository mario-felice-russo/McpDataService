using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace McpDataService.Services;

public class TrayIconService : IDisposable
{
    private static readonly string[] DatabaseOptions = ["SqlServer", "Oracle", "SQLite"];
    private readonly IConfiguration _configuration;
    private readonly AppSettingsService _appSettingsService;
    private readonly object _syncRoot = new();

    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _databaseMenuItem;
    private readonly Dictionary<string, ToolStripMenuItem> _databaseOptionItems = new(StringComparer.OrdinalIgnoreCase);
    private ApplicationContext? _appContext;
    private Form? _invokerForm;
    private Thread? _trayThread;
    private ManualResetEventSlim _startupSignal = new(false);
    private Exception? _startupException;
    private bool _disposed;

    public TrayIconService(IConfiguration configuration, AppSettingsService appSettingsService)
    {
        _configuration = configuration;
        _appSettingsService = appSettingsService;
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (_trayThread is { IsAlive: true })
            {
                return;
            }

            _startupException = null;
            _startupSignal.Dispose();
            _startupSignal = new ManualResetEventSlim(false);

            _trayThread = new Thread(RunTrayLoop)
            {
                IsBackground = true,
                Name = "McpDataService.TrayIcon"
            };

            _trayThread.SetApartmentState(ApartmentState.STA);
            _trayThread.Start();
        }

        var initialized = _startupSignal.Wait(TimeSpan.FromSeconds(10));
        if (!initialized)
        {
            throw new TimeoutException("Timeout during tray icon initialization.");
        }

        if (_startupException is not null)
        {
            throw new InvalidOperationException("Tray icon initialization failed.", _startupException);
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            if (_trayThread is null)
            {
                return;
            }
        }

        RunOnUiThread(() =>
        {
            DisposeNotifyIcon();
            _appContext?.ExitThread();
        });

        var thread = _trayThread;
        if (thread is not null && thread.IsAlive && thread != Thread.CurrentThread)
        {
            thread.Join(TimeSpan.FromSeconds(5));
        }

        lock (_syncRoot)
        {
            _trayThread = null;
        }
    }

    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        RunOnUiThread(() =>
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
        });
    }

    public void ApplyEnabledState(bool enabled, bool persistSetting = true)
    {
        if (persistSetting)
        {
            _appSettingsService.SetTrayIconEnabled(enabled);
        }

        if (enabled)
        {
            Start();
            return;
        }

        Stop();
    }

    public void ApplyCurrentDatabase(string database, bool persistSetting = true)
    {
        if (persistSetting)
        {
            _appSettingsService.SetCurrentDatabase(database);
        }

        var selected = _appSettingsService.GetCurrentDatabase();
        RunOnUiThread(() => UpdateDatabaseMenuSelection(selected));
    }

    private void RunTrayLoop()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _invokerForm = new Form
            {
                ShowInTaskbar = false,
                WindowState = FormWindowState.Minimized,
                Opacity = 0
            };

            _appContext = new ApplicationContext(_invokerForm);

            _notifyIcon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Visible = true,
                Text = "MCP Data Service"
            };

            CreateContextMenu();

            _startupSignal.Set();
            Application.Run(_appContext);
        }
        catch (Exception ex)
        {
            _startupException = ex;
            _startupSignal.Set();
        }
        finally
        {
            DisposeNotifyIcon();
            _invokerForm?.Dispose();
            _invokerForm = null;
            _appContext = null;
        }
    }

    private Icon LoadIcon()
    {
        var configuredPath = _configuration["TrayIcon:Path"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var resolvedPath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppContext.BaseDirectory, configuredPath);

            if (File.Exists(resolvedPath))
            {
                try
                {
                    return new Icon(resolvedPath);
                }
                catch
                {
                }
            }
        }
        return (Icon)SystemIcons.Application.Clone();
    }

    private void CreateContextMenu()
    {
        var contextMenu = new ContextMenuStrip();

        var swaggerItem = new ToolStripMenuItem("Swagger UI", null, (s, e) => OpenSwaggerWindow());
        var logsItem = new ToolStripMenuItem("Log Applicativi", null, (s, e) => OpenPage("/Logs"));
        var auditItem = new ToolStripMenuItem("Audit Logs", null, (s, e) => OpenPage("/AuditLogs"));
        var configItem = new ToolStripMenuItem("Configurazione", null, (s, e) => OpenPage("/Config"));
        var openItem = new ToolStripMenuItem("Apri Home", null, (s, e) => OpenPage("/"));
        var databaseMenuItem = CreateDatabaseSelectionMenu();
        var trayEnabledItem = new ToolStripMenuItem("Tray icon abilitata")
        {
            CheckOnClick = true,
            Checked = IsTrayIconEnabledInConfig()
        };
        var exitItem = new ToolStripMenuItem("Esci", null, (s, e) => ExitApplication());
        trayEnabledItem.Click += (_, _) => ToggleTrayIcon(trayEnabledItem);

        contextMenu.Items.AddRange(new ToolStripItem[]
        {
            openItem,
            new ToolStripSeparator(),
            trayEnabledItem,
            databaseMenuItem,
            new ToolStripSeparator(),
            swaggerItem,
            logsItem,
            auditItem,
            configItem,
            new ToolStripSeparator(),
            exitItem
        });

        _notifyIcon!.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => OpenPage("/");
    }

    private ToolStripMenuItem CreateDatabaseSelectionMenu()
    {
        var currentDatabase = _appSettingsService.GetCurrentDatabase();
        var databaseMenuItem = new ToolStripMenuItem($"Database corrente: {currentDatabase}");
        _databaseMenuItem = databaseMenuItem;
        _databaseOptionItems.Clear();

        foreach (var database in DatabaseOptions)
        {
            var optionItem = new ToolStripMenuItem(database)
            {
                Checked = string.Equals(database, currentDatabase, StringComparison.OrdinalIgnoreCase),
                CheckOnClick = false,
                Tag = database
            };
            _databaseOptionItems[database] = optionItem;

            optionItem.Click += (_, _) => SelectCurrentDatabase(database);
            databaseMenuItem.DropDownItems.Add(optionItem);
        }

        return databaseMenuItem;
    }

    private void SelectCurrentDatabase(string database)
    {
        try
        {
            ApplyCurrentDatabase(database, persistSetting: true);
            var selected = _appSettingsService.GetCurrentDatabase();
            ShowBalloon("MCP Data Service", $"Database corrente impostato su {selected}.", ToolTipIcon.Info);
        }
        catch
        {
            ShowBalloon("MCP Data Service", "Errore durante il salvataggio del database corrente.", ToolTipIcon.Error);
        }
    }

    private void UpdateDatabaseMenuSelection(string database)
    {
        if (_databaseMenuItem is null)
        {
            return;
        }

        foreach (var (name, item) in _databaseOptionItems)
        {
            item.Checked = string.Equals(name, database, StringComparison.OrdinalIgnoreCase);
        }

        _databaseMenuItem.Text = $"Database corrente: {database}";
    }

    private bool IsTrayIconEnabledInConfig()
    {
        return _configuration.GetValue<bool?>("TrayIcon:Enabled") ?? true;
    }

    private void ToggleTrayIcon(ToolStripMenuItem trayEnabledItem)
    {
        var enabled = trayEnabledItem.Checked;
        try
        {
            if (!enabled)
            {
                ShowBalloon("MCP Data Service", "Tray icon disabilitata.", ToolTipIcon.Info);
            }

            ApplyEnabledState(enabled, persistSetting: true);
        }
        catch
        {
            trayEnabledItem.Checked = IsTrayIconEnabledInConfig();
            ShowBalloon("MCP Data Service", "Errore durante l'aggiornamento della configurazione tray.", ToolTipIcon.Error);
        }
    }

    private void OpenPage(string path)
    {
        var baseUrl = GetBaseUrl();
        Process.Start(new ProcessStartInfo
        {
            FileName = $"{baseUrl.TrimEnd('/')}{path}",
            UseShellExecute = true
        });
    }

    private void OpenSwaggerWindow()
    {
        OpenPage("/SwaggerLauncher");
    }

    private string GetBaseUrl()
    {
        var urls = _configuration["ASPNETCORE_URLS"]
            ?? _configuration["DOTNET_URLS"]
            ?? _configuration["Urls"]
            ?? _configuration["Kestrel:Endpoints:Http:Url"]
            ?? "http://localhost:5000";

        var candidates = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (candidates.Length == 0)
        {
            return "http://localhost:5000";
        }

        var preferred = candidates.FirstOrDefault(url =>
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

        return preferred ?? candidates[0];
    }

    private void ExitApplication()
    {
        ShowBalloon("MCP Data Service", "Servizio in chiusura...", ToolTipIcon.Info);
        Stop();
        Environment.Exit(0);
    }

    private void RunOnUiThread(Action action)
    {
        var invoker = _invokerForm;
        if (invoker is null || invoker.IsDisposed)
        {
            return;
        }

        if (invoker.InvokeRequired)
        {
            invoker.BeginInvoke(action);
            return;
        }

        action();
    }

    private void DisposeNotifyIcon()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
        _databaseMenuItem = null;
        _databaseOptionItems.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();

        lock (_syncRoot)
        {
            _startupSignal.Dispose();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TrayIconService));
        }
    }
}
