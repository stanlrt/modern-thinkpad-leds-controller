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

// App.xaml.cs is the "composition root" — the one place where every object
// is created and wired together. Think of it as the circuit board that
// connects all the components.
//
// Now enhanced with dependency injection and comprehensive logging to diagnose
// issues when running as installed MSI vs. local development.
//
// Nothing in Hardware/, Monitoring/, Services/, or ViewModels/ knows about
// each other. They only know about their own dependencies (passed in via
// constructors). App.xaml.cs is the only place that sees everything.
public partial class App : System.Windows.Application
{
    // Dependency injection host for managing services and logging
    private IHost? _host;
    private ILogger<App>? _logger;

    // We keep references so Dispose() is called on shutdown.
    private LhmDriver? _driver;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        // Emergency logging in case something fails before Serilog initializes
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
            catch { }
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
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            _logger.LogDebug("Exception handlers registered");

            if (!TryInitializeSingleInstance()) return;
            if (!TryInitializeDriver()) return;

            InitializeHardwareAndMonitoring(out var leds, out var diskOk);
            InitializeViewModelsAndUI(leds, diskOk);
            WireUpEventHandlers();
            StartMonitors();

            // --- Show or hide on startup ---
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

            // Catch any unhandled startup exceptions
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
                // Register services here if needed
                // For now, we're using manual instantiation in OnStartup
                // but this allows us to use ILogger<T> properly
            });
    }

    private bool TryInitializeSingleInstance()
    {
        _logger?.LogDebug("Checking for existing application instance");

        _singleInstanceMutex = new System.Threading.Mutex(
            initiallyOwned: true,
            name: "ModernThinkPadLEDsController_SingleInstance",
            out bool isFirstInstance);

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

    private bool TryInitializeDriver()
    {
        _logger?.LogInformation("Initializing LHM driver (LibreHardwareMonitor/PawnIO)");

        if (!LhmDriver.TryOpen(out _driver))
        {
            _logger?.LogError("Failed to open LHM driver - driver not available or not installed");
            _logger?.LogInformation("Showing PawnIO setup window to user");

            var setup = new PawnIOSetupWindow();
            setup.ShowDialog();

            _logger?.LogInformation("PawnIO setup window closed - shutting down application");
            // User either cancelled or verification succeeded (with auto-restart or manual restart message)
            // Either way, exit this instance
            Shutdown();
            return false;
        }

        _logger?.LogInformation("LHM driver initialized successfully");
        return true;
    }

    private void InitializeHardwareAndMonitoring(out LedController leds, out bool diskOk)
    {
        _logger?.LogInformation("Initializing hardware controllers and monitoring services");

        var ec = new EcController(_driver!);
        _logger?.LogDebug("EcController created");

        leds = new LedController(ec);
        _logger?.LogDebug("LedController created");

        _settings = AppSettings.Load();
        _logger?.LogInformation("Settings loaded from: {SettingsPath}", _settings.GetType().Name);
        _logger?.LogDebug("HDD poll interval: {PollInterval}ms", _settings.HddPollIntervalMs);

        _diskMonitor = new DiskActivityMonitor(_settings.HddPollIntervalMs);
        _kbdMonitor = new KeyboardBacklightMonitor(leds);
        _micMonitor = new MicrophoneMuteMonitor();
        _powerListener = new PowerEventListener();
        _logger?.LogDebug("Monitor services instantiated");

        diskOk = _diskMonitor.TryInitialize();
        if (diskOk)
            _logger?.LogInformation("Disk activity monitor initialized successfully");
        else
            _logger?.LogWarning("Disk activity monitor initialization failed - disk LED features will be unavailable");
    }

    private void InitializeViewModelsAndUI(LedController leds, bool diskOk)
    {
        _logger?.LogInformation("Initializing view models and UI");

        _mainVm = new MainViewModel(leds, _settings!);
        _settingsVm = new SettingsViewModel(_settings!, _diskMonitor!, _kbdMonitor!, _powerListener!, leds);
        _logger?.LogDebug("View models created");

        _mainVm.LoadFrom(_settings!);
        _settingsVm.LoadFrom(_settings!);
        _logger?.LogDebug("Settings loaded into view models");

        _mainVm.DiskMonitoringAvailable = diskOk;
        _mainVm.ApplyAll();
        _logger?.LogInformation("LED configurations applied");

        // Wire up save callbacks for automatic persistence
        _mainVm.SetSaveCallback(SaveSettings);
        _settingsVm.SetSaveCallback(SaveSettings);
        _logger?.LogDebug("Save callbacks configured");

        _tray = new TrayIconService();
        _tray.Initialize();
        _tray.ShowWindowRequested += ShowMainWindow;
        _tray.ExitRequested += RequestExit;
        _logger?.LogInformation("System tray icon initialized");

        _mainWindow = new MainWindow(_mainVm, _settingsVm);
        _logger?.LogDebug("Main window created");

        if (_mainVm.HasDiskModeLeds && diskOk)
        {
            _settingsVm.StartDiskMonitoring();
            _logger?.LogInformation("Disk monitoring started (has disk mode LEDs)");
        }
    }

    private void WireUpEventHandlers()
    {
        _logger?.LogDebug("Wiring up event handlers");

        _mainVm!.DiskModeLedsChanged += hasDiskModes =>
            Dispatcher.Invoke(() =>
            {
                _logger?.LogDebug("Disk mode LEDs changed: {HasDiskModes}", hasDiskModes);
                if (hasDiskModes) _settingsVm!.StartDiskMonitoring();
                else _settingsVm!.StopDiskMonitoring();
            });

        _mainWindow!.SourceInitialized += (_, _) =>
        {
            _logger?.LogDebug("Main window source initialized");
            _powerListener!.Attach(_mainWindow);
            _hotkey = new HotkeyService();
            _hotkey.Register(_mainWindow);
            _hotkey.HotkeyPressed += () => Dispatcher.Invoke(() => _mainVm.OnHotkeyPressed());
            _logger?.LogInformation("Hotkey service initialized");
        };

        _diskMonitor!.StateChanged += state =>
            Dispatcher.Invoke(() =>
            {
                _logger?.LogDebug("Disk state changed: {State}", state);
                _tray!.SetDiskState(state);
                _mainVm.OnDiskStateChanged(state);
            });

        _micMonitor!.MuteStateChanged += isMuted =>
            Dispatcher.Invoke(() =>
            {
                _logger?.LogDebug("Microphone mute state changed: {IsMuted}", isMuted);
                _mainVm.OnMicrophoneMuteChanged(isMuted);
            });

        _powerListener!.SystemSuspending += () =>
            Dispatcher.Invoke(() =>
            {
                _logger?.LogInformation("System suspending - stopping monitors");
                _kbdMonitor!.Stop();
                _diskMonitor.Stop();
            });

        _powerListener.SystemResumed += () =>
            Dispatcher.Invoke(() =>
            {
                _logger?.LogInformation("System resumed - restarting monitors");
                if (_settings!.RememberKeyboardBacklight)
                    _kbdMonitor!.RestoreMostCommonLevel();
                if (_mainVm.HasDiskModeLeds)
                    _diskMonitor.Start();
                _kbdMonitor!.Start();
            });

        _powerListener.LidStateChanged += isOpen =>
            Dispatcher.Invoke(() =>
            {
                _logger?.LogDebug("Lid state changed: {IsOpen}", isOpen);
                if (isOpen && _settings!.RememberKeyboardBacklight)
                    _kbdMonitor!.RestoreMostCommonLevel();
            });

        _powerListener.FullscreenChanged += isFullscreen =>
            Dispatcher.Invoke(() =>
            {
                _logger?.LogDebug("Fullscreen state changed: {IsFullscreen}", isFullscreen);
                _mainVm.OnFullscreenChanged(isFullscreen, _kbdMonitor!.CurrentLevel);
            });

        _logger?.LogInformation("Event handlers wired up successfully");
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

            _logger?.LogDebug("Disposing monitors and services");
            _diskMonitor?.Dispose();
            _kbdMonitor?.Dispose();
            _micMonitor?.Dispose();
            _powerListener?.Dispose();
            _hotkey?.Dispose();
            _tray?.Dispose();
            _driver?.Dispose();

            _logger?.LogDebug("Releasing single instance mutex");
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();

            _host?.Dispose();
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

    // ── Global exception handlers ──
    // These catch exceptions that would otherwise crash the app silently or
    // cause the 0xC000041D "unhandled exception in user callback" error.
    // Now integrated with structured logging.

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled exception on UI thread");
        LogAndShowException("UI Thread Exception", e.Exception);
        e.Handled = true; // Prevent crash
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

    private void LogAndShowException(string title, Exception ex)
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

        // Show to user
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

}

