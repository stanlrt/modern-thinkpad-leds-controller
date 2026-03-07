using System.Windows;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Logging;
using ModernThinkPadLEDsController.Monitoring;
using ModernThinkPadLEDsController.Services;
using ModernThinkPadLEDsController.ViewModels;
using ModernThinkPadLEDsController.Views;
using Serilog;

namespace ModernThinkPadLEDsController;

/// <summary>Root app manager.</summary>
public partial class App : System.Windows.Application
{
    // Dependency injection host for managing services and logging
    private IHost? _host;
    private ILogger<App>? _logger;

    // We keep references for services we need to access after construction.
    // DI host handles disposal automatically on shutdown.
    private DiskActivityMonitor? _diskMonitor;
    private KeyboardBacklightMonitor? _kbdMonitor;
    private MicrophoneMuteMonitor? _micMonitor;
    private PowerEventListener? _powerListener;
    private HotkeyService? _hotkey;
    private TrayIconService? _tray;
    private AppSettings? _settings;
    private MainViewModel? _mainVm;
    private SettingsViewModel? _settingsVm;
    private MainWindow? _mainWindow;

    // A named Mutex prevents two instances of the app running simultaneously.
    private System.Threading.Mutex? _singleInstanceMutex;
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

            _logger.LogInformation("Application startup initiated");
            _logger.LogDebug("Command line arguments: {Args}", string.Join(" ", e.Args));
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
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("LHM driver"))
            {
                _logger.LogError("Failed to initialize driver - showing PawnIO setup window");
                var setup = new PawnIOSetupWindow();
                setup.ShowDialog();
                Shutdown();
                return;
            }

            InitializeServicesAndUI();
            WireUpEventHandlers();
            StartMonitors();

            bool startMinimized = e.Args.Contains("--minimized");
            _logger.LogInformation("Start minimized: {StartMinimized}", startMinimized);

            if (!startMinimized)
            {
                _mainWindow!.Show();
                _logger.LogInformation("Main window shown");
            }
            else
            {
                _logger.LogInformation("Started minimized to system tray");
            }

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
                services.AddSingleton<AppSettings>(sp => AppSettings.Load());

                // Hardware layer - driver must be validated before use
                services.AddSingleton<LhmDriver>(sp =>
                {
                    if (!LhmDriver.TryOpen(out var driver) || driver is null)
                        throw new InvalidOperationException("Failed to initialize LHM driver");
                    return driver;
                });
                // Register LhmDriver as IPortIO so EcController can resolve it
                services.AddSingleton<IPortIO>(sp => sp.GetRequiredService<LhmDriver>());

                services.AddSingleton<EcController>();
                services.AddSingleton<LedController>();

                // Monitoring services
                services.AddSingleton<DiskActivityMonitor>(sp =>
                {
                    var settings = sp.GetRequiredService<AppSettings>();
                    var monitor = new DiskActivityMonitor(settings.HddPollIntervalMs);
                    monitor.TryInitialize(); // Initialize on creation
                    return monitor;
                });
                services.AddSingleton<KeyboardBacklightMonitor>();
                services.AddSingleton<MicrophoneMuteMonitor>();
                services.AddSingleton<PowerEventListener>();

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
            });
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
            _logger?.LogWarning("Another instance is already running");
            System.Windows.MessageBox.Show("Modern ThinkPad LEDs Controller is already running.\n\nLook for its icon in the system tray.",
                            "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return false;
        }

        _logger?.LogInformation("Single instance check passed - no other instance detected");
        return true;
    }

    /// <summary>
    /// Resolve all services from the DI container.
    /// </summary>
    private void ResolveServices()
    {
        _logger?.LogInformation("Resolving services from DI container");

        // Resolve all services - this triggers construction and validation
        // Note: LhmDriver is resolved implicitly through dependencies, no need to store reference
        _settings = _host!.Services.GetRequiredService<AppSettings>();
        _diskMonitor = _host.Services.GetRequiredService<DiskActivityMonitor>();
        _kbdMonitor = _host.Services.GetRequiredService<KeyboardBacklightMonitor>();
        _micMonitor = _host.Services.GetRequiredService<MicrophoneMuteMonitor>();
        _powerListener = _host.Services.GetRequiredService<PowerEventListener>();
        _tray = _host.Services.GetRequiredService<TrayIconService>();
        _mainVm = _host.Services.GetRequiredService<MainViewModel>();
        _settingsVm = _host.Services.GetRequiredService<SettingsViewModel>();
        _mainWindow = _host.Services.GetRequiredService<MainWindow>();

        _logger?.LogInformation("All services resolved successfully");
    }

    /// <summary>
    /// Initialize services that need post-construction setup.
    /// </summary>
    private void InitializeServicesAndUI()
    {
        _logger?.LogInformation("Initializing services and UI");

        // Check if disk monitoring is available
        // Note: DiskActivityMonitor.TryInitialize() was already called during service registration
        // We just need to check if it's working
        bool diskOk = true; // Assume it worked; monitor will log if it didn't

        // Load settings into view models
        _mainVm!.LoadFrom(_settings!);
        _settingsVm!.LoadFrom(_settings!);
        _logger?.LogDebug("Settings loaded into view models");

        // Configure ViewModels
        _mainVm.DiskMonitoringAvailable = diskOk;
        _mainVm.ApplyAll();
        _logger?.LogInformation("LED configurations applied");

        // Wire up save callbacks for automatic persistence
        _mainVm.SetSaveCallback(SaveSettings);
        _settingsVm.SetSaveCallback(SaveSettings);
        _logger?.LogDebug("Save callbacks configured");

        // Configure tray icon
        _tray!.ShowWindowRequested += ShowMainWindow;
        _tray.ExitRequested += RequestExit;
        _logger?.LogInformation("System tray icon configured");

        _logger?.LogDebug("Main window ready");

        if (_mainVm.HasDiskModeLeds)
        {
            _settingsVm.StartDiskMonitoring();
            _logger?.LogInformation("Disk monitoring started (has disk mode LEDs)");
        }
    }

    private void WireUpEventHandlers()
    {
        _logger?.LogDebug("Wiring up event handlers");

        _mainVm!.DiskModeLedsChanged += OnDiskModeLedsChanged;
        _mainWindow!.SourceInitialized += OnMainWindowSourceInitialized;
        _diskMonitor!.StateChanged += OnDiskStateChanged;
        _micMonitor!.MuteStateChanged += OnMicrophoneMuteStateChanged;
        _powerListener!.SystemSuspending += OnSystemSuspending;
        _powerListener.SystemResumed += OnSystemResumed;
        _powerListener.LidStateChanged += OnLidStateChanged;
        _powerListener.FullscreenChanged += OnFullscreenChanged;

        _logger?.LogInformation("Event handlers wired up successfully");
    }

    private void OnDiskModeLedsChanged(bool hasDiskModes)
    {
        Dispatcher.Invoke(() =>
        {
            _logger?.LogDebug("Disk mode LEDs changed: {HasDiskModes}", hasDiskModes);
            if (hasDiskModes)
                _settingsVm!.StartDiskMonitoring();
            else
                _settingsVm!.StopDiskMonitoring();
        });
    }

    private void OnMainWindowSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not MainWindow window) return;

        _logger?.LogDebug("Main window source initialized");
        _powerListener!.Attach(window);
        _hotkey = new HotkeyService();
        _hotkey.Register(window);
        _hotkey.HotkeyPressed += OnHotkeyPressed;
        _logger?.LogInformation("Hotkey service initialized");
    }

    private void OnHotkeyPressed()
    {
        Dispatcher.Invoke(() => _mainVm!.OnHotkeyPressed());
    }

    private void OnDiskStateChanged(DiskActivityState state)
    {
        Dispatcher.Invoke(() =>
        {
            _logger?.LogDebug("Disk state changed: {State}", state);
            _mainVm!.OnDiskStateChanged(state);
        });
    }

    private void OnMicrophoneMuteStateChanged(bool isMuted)
    {
        Dispatcher.Invoke(() =>
        {
            _logger?.LogDebug("Microphone mute state changed: {IsMuted}", isMuted);
            _mainVm!.OnMicrophoneMuteChanged(isMuted);
        });
    }

    private void OnSystemSuspending()
    {
        Dispatcher.Invoke(() =>
        {
            _logger?.LogInformation("System suspending - stopping monitors");
            _kbdMonitor!.Stop();
            _diskMonitor!.Stop();
        });
    }

    private void OnSystemResumed()
    {
        Dispatcher.Invoke(() =>
        {
            _logger?.LogInformation("System resumed - restarting monitors");
            if (_settings!.RememberKeyboardBacklight)
                _kbdMonitor!.RestoreMostCommonLevel();
            if (_mainVm!.HasDiskModeLeds)
                _diskMonitor!.Start();
            _kbdMonitor!.Start();
        });
    }

    private void OnLidStateChanged(bool isOpen)
    {
        Dispatcher.Invoke(() =>
        {
            _logger?.LogDebug("Lid state changed: {IsOpen}", isOpen);
            if (isOpen && _settings!.RememberKeyboardBacklight)
                _kbdMonitor!.RestoreMostCommonLevel();
        });
    }

    private void OnFullscreenChanged(bool isFullscreen)
    {
        Dispatcher.Invoke(() =>
        {
            _logger?.LogDebug("Fullscreen state changed: {IsFullscreen}", isFullscreen);
            _mainVm!.OnFullscreenChanged(isFullscreen, _kbdMonitor!.CurrentLevel);
        });
    }

    private void StartMonitors()
    {
        _logger?.LogInformation("Starting monitors");

        if (_settings!.RememberKeyboardBacklight)
        {
            _kbdMonitor!.Start();
            _logger?.LogDebug("Keyboard backlight monitor started");
        }

        if (_settings.DimLedsWhenFullscreen)
        {
            _powerListener!.StartFullscreenPolling();
            _logger?.LogDebug("Fullscreen polling started");
        }

        _micMonitor!.Start();
        _logger?.LogDebug("Microphone mute monitor started");

        var isMuted = _micMonitor.QueryMuted();
        _mainVm!.OnMicrophoneMuteChanged(isMuted);
        _logger?.LogDebug("Initial microphone mute state: {IsMuted}", isMuted);
    }

    private void ShowMainWindow()
    {
        _logger?.LogDebug("ShowMainWindow requested");
        if (_mainWindow is null)
        {
            _logger?.LogWarning("Cannot show main window - window is null");
            return;
        }
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _logger?.LogInformation("Main window shown and activated");
    }

    private void RequestExit()
    {
        _logger?.LogInformation("Exit requested - beginning shutdown sequence");

        try
        {
            SaveSettings();

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
            throw;
        }
    }

    private void SaveSettings()
    {
        try
        {
            _logger?.LogDebug("Saving settings");
            _mainVm?.SaveTo(_settings!);
            _settingsVm?.SaveTo(_settings!);
            _settings?.Save();
            _logger?.LogDebug("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save settings");
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

