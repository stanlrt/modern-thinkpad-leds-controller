using System.Windows;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.Logging;
using ModernThinkPadLEDsController.Monitoring;
using ModernThinkPadLEDsController.Presentation.Services;
using ModernThinkPadLEDsController.Presentation.ViewModels;
using ModernThinkPadLEDsController.Presentation.Views;
using ModernThinkPadLEDsController.Runtime;
using ModernThinkPadLEDsController.Settings;
using ModernThinkPadLEDsController.Shell;
using Serilog;

namespace ModernThinkPadLEDsController;

/// <summary>Root app manager.</summary>
public partial class App : System.Windows.Application
{
    private static readonly string[] SafeModeArguments = ["--safe-mode", "--no-hardware"];

    // Win32 API imports for activating existing window instance
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    private const int SW_RESTORE = 9;

    // Dependency injection host for managing services and logging
    private IHost? _host;
    private ILogger<App>? _logger;
    private ApplicationCoordinator? _app;
    private ShellCoordinator? _shell;
    private MainPresentationService? _presentation;
    private SettingsPersistenceService? _settingsPersistence;

    // A named Mutex prevents two instances of the app running simultaneously.
    private Mutex? _singleInstanceMutex;
    private bool _mutexOwned = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        var emergencyLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModernThinkPadLEDsController",
            "Logs",
            "emergency.log");

        void EmergencyLog(string message)
        {
            try
            {
                var dir = Path.GetDirectoryName(emergencyLogPath);
                if (dir != null) Directory.CreateDirectory(dir);
                File.AppendAllText(emergencyLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [App.OnStartup] {message}\n");
            }
            catch
            {
                // If we can't even write to the emergency log, there's not much else we can do
            }
        }

        try
        {
            EmergencyLog("=== OnStartup() called ===");
            EmergencyLog($"Args: {string.Join(" ", e.Args)}");

            // ══════════════════════════════════════════════════════════════
            // CRITICAL: Initialize logging FIRST before anything else
            // This ensures all subsequent operations including errors are logged
            // ══════════════════════════════════════════════════════════════
            EmergencyLog("Calling ConfigureSerilog()...");
            LoggingConfiguration.ConfigureSerilog();
            EmergencyLog("ConfigureSerilog() completed");

            base.OnStartup(e);
            EmergencyLog("base.OnStartup() completed");

            // Build dependency injection host
            _host = CreateHostBuilder(e.Args).Build();
            _logger = _host.Services.GetRequiredService<ILogger<App>>();
            var hardwareAccess = _host.Services.GetRequiredService<HardwareAccessController>();

            _logger.LogDebug("Application startup initiated");
            _logger.LogDebug("Command line arguments: {Args}", string.Join(" ", e.Args));
            _logger.LogInformation("Hardware access status: {Status}", hardwareAccess.GetStatusDescription());
            LoggingConfiguration.LogEnvironmentDetails();

            // Global exception handlers to prevent silent crashes
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            _logger.LogDebug("Exception handlers registered");

            if (!TryInitializeSingleInstance()) return;

            try
            {
                ResolveServices();
                _app!.ExitRequested += RequestExit;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("LHM driver"))
            {
                _logger.LogError(ex, "Failed to initialize driver - showing PawnIO setup window");
                var setup = new PawnIOSetupWindow();
                setup.ShowDialog();
                Shutdown();
                return;
            }

            _app.Initialize();
            _app.EnsureMainWindowHandle();

            bool startMinimized = e.Args.Contains("--minimized");
            _app.Start(startMinimized);

            _logger.LogInformation("Application startup completed successfully");
            EmergencyLog("=== OnStartup() completed successfully ===");
        }
        catch (Exception ex)
        {
            EmergencyLog($"FATAL EXCEPTION in OnStartup(): {ex.GetType().Name}: {ex.Message}");
            EmergencyLog($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                EmergencyLog($"Inner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                EmergencyLog($"Inner Stack Trace: {ex.InnerException.StackTrace}");
            }

            Log.Fatal(ex, "Fatal error during application startup");

            var message = $"Fatal startup error:\n\n{ex.GetType().Name}: {ex.Message}\n\n" +
                         $"Stack Trace:\n{ex.StackTrace}\n\n" +
                         $"See logs at: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModernThinkPadLEDsController", "Logs")}";

            System.Windows.MessageBox.Show(message, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);

            LoggingConfiguration.CloseAndFlush();
            Shutdown();
        }
    }

    /// <summary>
    /// Creates the dependency injection host for the application.
    /// This configures all services and logging infrastructure.
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureLogging()
            .ConfigureServices((context, services) =>
            {
                AppSettings settings = AppSettings.Load();
                bool startedInSafeMode = args.Any(IsSafeModeArgument);
                bool startWithHardwareAccess = settings.EnableHardwareAccess && !startedInSafeMode;
                string? startupReason = null;
                if (!startWithHardwareAccess)
                {
                    startupReason = startedInSafeMode
                        ? "Started with --safe-mode."
                        : "Disabled in app settings.";
                }

                services.AddSingleton(settings);
                services.AddSingleton(new HardwareAccessController(
                    isEnabled: startWithHardwareAccess,
                    driverLoaded: startWithHardwareAccess,
                    startupReason: startupReason));

                // Hardware layer - driver must be validated before use
                if (startWithHardwareAccess)
                {
                    services.AddSingleton<LhmDriver>(sp =>
                    {
                        if (!LhmDriver.TryOpen(out var driver) || driver is null)
                            throw new InvalidOperationException("Failed to initialize LHM driver");
                        return driver;
                    });

                    // Register LhmDriver as IPortIO so EcController can resolve it
                    services.AddSingleton<IPortIO>(sp => sp.GetRequiredService<LhmDriver>());
                }
                else
                {
                    services.AddSingleton<IPortIO, NoOpPortIO>();
                }

                services.AddSingleton<EcController>();
                services.AddSingleton<LedController>();
                services.AddSingleton<HotkeyService>();
                services.AddSingleton<LedBehaviorService>();
                services.AddSingleton<ISettingsRuntimeService, SettingsRuntimeService>();

                // Monitoring services
                services.AddSingleton<DiskActivityMonitor>(sp =>
                {
                    var settings = sp.GetRequiredService<AppSettings>();
                    var monitor = new DiskActivityMonitor(settings.DiskPollIntervalMs);
                    monitor.TryInitialize(); // Initialize on creation
                    return monitor;
                });
                services.AddSingleton<KeyboardBacklightMonitor>();
                services.AddSingleton<MicrophoneMuteMonitor>();
                services.AddSingleton<SpeakerMuteMonitor>();
                services.AddSingleton<PowerEventMonitor>();
                services.AddSingleton<FullscreenMonitor>();

                // UI services
                services.AddSingleton<TrayIconService>(sp =>
                {
                    var tray = new TrayIconService();
                    tray.Initialize();
                    return tray;
                });

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<SettingsViewModel>();

                // Main window
                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainWindowHost>();
                services.AddSingleton<MainPresentationService>();
                services.AddSingleton<SettingsPersistenceService>();
                services.AddSingleton<ShellCoordinator>();
                services.AddSingleton<HardwareRuntimeCoordinator>();
                services.AddSingleton<ApplicationCoordinator>();
            });
    }

    private static bool IsSafeModeArgument(string arg)
    {
        return SafeModeArguments.Any(safeModeArg => string.Equals(arg, safeModeArg, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryInitializeSingleInstance()
    {
        _logger?.LogDebug("Checking for existing application instance");

        _singleInstanceMutex = new System.Threading.Mutex(
            initiallyOwned: true,
            name: "ModernThinkPadLEDsController_SingleInstance",
            out bool isFirstInstance);

        _mutexOwned = isFirstInstance;

        if (!isFirstInstance)
        {
            _logger?.LogWarning("Another instance is already running - attempting to activate it");

            // Try to find and activate the existing window
            if (TryActivateExistingInstance())
            {
                _logger?.LogInformation("Successfully activated existing instance window");
            }
            else
            {
                _logger?.LogWarning("Could not find/activate existing window - showing message");
                System.Windows.MessageBox.Show(
                    "Modern ThinkPad LEDs Controller is already running.\n\nLook for its icon in the system tray.",
                    "Already Running",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            Shutdown();
            return false;
        }

        _logger?.LogInformation("Single instance check passed - no other instance detected");
        return true;
    }

    /// <summary>
    /// Attempts to find and activate the main window of an existing application instance.
    /// </summary>
    /// <returns>True if the window was found and activated; otherwise false.</returns>
    private bool TryActivateExistingInstance()
    {
        try
        {
            // Try to find the window by its title
            // MainWindow's title is "Modern ThinkPad LEDs Controller"
            IntPtr hWnd = FindWindow(null, "Modern ThinkPad LEDs Controller");

            if (hWnd == IntPtr.Zero)
            {
                _logger?.LogDebug("Could not find existing window by title");
                return false;
            }

            _logger?.LogDebug("Found existing window handle: {Handle}", hWnd);

            if (IsIconic(hWnd))
                _logger?.LogDebug("Window is minimized - restoring");

            // Restore also covers the tray-hidden case better than only checking iconic state.
            ShowWindow(hWnd, SW_RESTORE);

            // Bring the window to the foreground
            bool success = SetForegroundWindow(hWnd);
            _logger?.LogDebug("SetForegroundWindow result: {Success}", success);

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while trying to activate existing instance");
            return false;
        }
    }

    /// <summary>
    /// Resolve all services from the DI container.
    /// </summary>
    private void ResolveServices()
    {
        _logger?.LogInformation("Resolving services from DI container");

        // Resolve all services - this triggers construction and validation
        _app = _host!.Services.GetRequiredService<ApplicationCoordinator>();
        _shell = _host.Services.GetRequiredService<ShellCoordinator>();
        _presentation = _host.Services.GetRequiredService<MainPresentationService>();
        _settingsPersistence = _host.Services.GetRequiredService<SettingsPersistenceService>();

        _logger?.LogInformation("All services resolved successfully");
    }

    // -------------------------------------------------------------------------
    // Hotkey management
    // -------------------------------------------------------------------------

    /// <summary>
    /// Updates the registered hotkey and saves it to settings.
    /// </summary>
    /// <returns>True if the hotkey was registered successfully; false if already in use</returns>
    public bool UpdateHotkey(HotkeyModifiers modifiers, Key key, string displayText)
    {
        return _shell?.UpdateHotkey(modifiers, key, displayText) ?? false;
    }

    /// <summary>
    /// Gets the current hotkey display text from settings.
    /// </summary>
    public string GetHotkeyDisplayText()
    {
        if (_presentation is null || _settingsPersistence is null)
            return string.Empty;

        return _presentation.FormatHotkeyDisplay(
            _settingsPersistence.HotkeyModifiers,
            _settingsPersistence.HotkeyKey);
    }

    private void RequestExit()
    {
        _logger?.LogInformation("Exit requested - beginning shutdown sequence");

        try
        {
            // Only save if auto-save is enabled; otherwise discard temporary changes
            if (_settingsPersistence?.PersistSettingsOnChange ?? false)
            {
                _settingsPersistence.SaveCurrentSettings();
            }
            else
            {
                _logger?.LogInformation("PersistSettingsOnChange is disabled - discarding unsaved changes");
            }

            _logger?.LogDebug("Disposing DI host (will dispose all services automatically)");
            _host?.Dispose(); // This disposes all registered services

            _logger?.LogDebug("Releasing single instance mutex");
            if (_mutexOwned)
            {
                _singleInstanceMutex?.ReleaseMutex();
            }
            _singleInstanceMutex?.Dispose();

            _logger?.LogInformation("Shutdown sequence completed");

            LoggingConfiguration.CloseAndFlush();
            Shutdown();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during shutdown sequence");
            LoggingConfiguration.CloseAndFlush();
            throw new InvalidOperationException("Error during shutdown sequence", ex);
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled exception on UI thread");
        LogAndShowException("UI Thread Exception", e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            _logger?.LogCritical(ex, "Unhandled exception on background thread. IsTerminating: {IsTerminating}", e.IsTerminating);
            LogAndShowException("Background Thread Exception", ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unobserved task exception");
        LogAndShowException("Task Exception", e.Exception);
        e.SetObserved(); // Prevent crash
    }

    private static void LogAndShowException(string title, Exception ex)
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModernThinkPadLEDsController",
            "Logs");

        var message = $"{ex.GetType().Name}: {ex.Message}\n\n" +
                     $"Stack Trace:\n{ex.StackTrace}\n\n" +
                     $"═══════════════════════════════════════════\n" +
                     $"Logs are saved to:\n{logDirectory}\n" +
                     $"═══════════════════════════════════════════";

        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

}

