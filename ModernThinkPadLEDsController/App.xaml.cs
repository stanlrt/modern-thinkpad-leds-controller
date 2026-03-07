using System.Windows;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Monitoring;
using ModernThinkPadLEDsController.Services;
using ModernThinkPadLEDsController.ViewModels;
using ModernThinkPadLEDsController.Views;

namespace ModernThinkPadLEDsController;

// App.xaml.cs is the "composition root" — the one place where every object
// is created and wired together. Think of it as the circuit board that
// connects all the components.
//
// Nothing in Hardware/, Monitoring/, Services/, or ViewModels/ knows about
// each other. They only know about their own dependencies (passed in via
// constructors). App.xaml.cs is the only place that sees everything.
public partial class App : System.Windows.Application
{
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
        base.OnStartup(e);

        // Global exception handlers to prevent silent crashes
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (!TryInitializeSingleInstance()) return;
        if (!TryInitializeDriver()) return;

        InitializeHardwareAndMonitoring(out var leds, out var diskOk);
        InitializeViewModelsAndUI(leds, diskOk);
        WireUpEventHandlers();
        StartMonitors();

        // --- Show or hide on startup ---
        bool startMinimized = e.Args.Contains("--minimized");
        if (!startMinimized)
            _mainWindow!.Show();
    }

    private bool TryInitializeSingleInstance()
    {
        _singleInstanceMutex = new System.Threading.Mutex(
            initiallyOwned: true,
            name: "ModernThinkPadLEDsController_SingleInstance",
            out bool isFirstInstance);

        if (!isFirstInstance)
        {
            System.Windows.MessageBox.Show("Modern ThinkPad LEDs Controller is already running.\n\nLook for its icon in the system tray.",
                            "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return false;
        }
        return true;
    }

    private bool TryInitializeDriver()
    {
        if (!LhmDriver.TryOpen(out _driver))
        {
            var setup = new PawnIOSetupWindow();
            setup.ShowDialog();

            // User either cancelled or verification succeeded (with auto-restart or manual restart message)
            // Either way, exit this instance
            Shutdown();
            return false;
        }

        return true;
    }

    private void InitializeHardwareAndMonitoring(out LedController leds, out bool diskOk)
    {
        var ec = new EcController(_driver!);
        leds = new LedController(ec);

        _settings = AppSettings.Load();

        _diskMonitor = new DiskActivityMonitor(_settings.HddPollIntervalMs);
        _kbdMonitor = new KeyboardBacklightMonitor(leds);
        _micMonitor = new MicrophoneMuteMonitor();
        _powerListener = new PowerEventListener();

        diskOk = _diskMonitor.TryInitialize();
    }

    private void InitializeViewModelsAndUI(LedController leds, bool diskOk)
    {
        _mainVm = new MainViewModel(leds, _settings!);
        _settingsVm = new SettingsViewModel(_settings!, _diskMonitor!, _kbdMonitor!, _powerListener!, leds);

        _mainVm.LoadFrom(_settings!);
        _settingsVm.LoadFrom(_settings!);
        _mainVm.DiskMonitoringAvailable = diskOk;
        _mainVm.ApplyAll();

        // Wire up save callbacks for automatic persistence
        _mainVm.SetSaveCallback(SaveSettings);
        _settingsVm.SetSaveCallback(SaveSettings);

        _tray = new TrayIconService();
        _tray.Initialize();
        _tray.ShowWindowRequested += ShowMainWindow;
        _tray.ExitRequested += RequestExit;

        _mainWindow = new MainWindow(_mainVm, _settingsVm);

        if (_mainVm.HasDiskModeLeds && diskOk)
            _settingsVm.StartDiskMonitoring();
    }

    private void WireUpEventHandlers()
    {
        _mainVm!.DiskModeLedsChanged += hasDiskModes =>
            Dispatcher.Invoke(() =>
            {
                if (hasDiskModes) _settingsVm!.StartDiskMonitoring();
                else _settingsVm!.StopDiskMonitoring();
            });

        _mainWindow!.SourceInitialized += (_, _) =>
        {
            _powerListener!.Attach(_mainWindow);
            _hotkey = new HotkeyService();
            _hotkey.Register(_mainWindow);
            _hotkey.HotkeyPressed += () => Dispatcher.Invoke(() => _mainVm.OnHotkeyPressed());
        };

        _diskMonitor!.StateChanged += state =>
            Dispatcher.Invoke(() =>
            {
                _tray!.SetDiskState(state);
                _mainVm.OnDiskStateChanged(state);
            });

        _micMonitor!.MuteStateChanged += isMuted =>
            Dispatcher.Invoke(() => _mainVm.OnMicrophoneMuteChanged(isMuted));

        _powerListener!.SystemSuspending += () =>
            Dispatcher.Invoke(() =>
            {
                _kbdMonitor!.Stop();
                _diskMonitor.Stop();
            });

        _powerListener.SystemResumed += () =>
            Dispatcher.Invoke(() =>
            {
                if (_settings!.RememberKeyboardBacklight)
                    _kbdMonitor!.RestoreMostCommonLevel();
                if (_mainVm.HasDiskModeLeds)
                    _diskMonitor.Start();
                _kbdMonitor!.Start();
            });

        _powerListener.LidStateChanged += isOpen =>
            Dispatcher.Invoke(() =>
            {
                if (isOpen && _settings!.RememberKeyboardBacklight)
                    _kbdMonitor!.RestoreMostCommonLevel();
            });

        _powerListener.FullscreenChanged += isFullscreen =>
            Dispatcher.Invoke(() =>
                _mainVm.OnFullscreenChanged(isFullscreen, _kbdMonitor!.CurrentLevel));
    }

    private void StartMonitors()
    {
        if (_settings!.RememberKeyboardBacklight) _kbdMonitor!.Start();
        if (_settings.DimLedsWhenFullscreen) _powerListener!.StartFullscreenPolling();

        _micMonitor!.Start();
        _mainVm!.OnMicrophoneMuteChanged(_micMonitor.QueryMuted());
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void RequestExit()
    {
        SaveSettings();

        _diskMonitor?.Dispose();
        _kbdMonitor?.Dispose();
        _micMonitor?.Dispose();
        _powerListener?.Dispose();
        _hotkey?.Dispose();
        _tray?.Dispose();
        _driver?.Dispose();

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        Shutdown();
    }

    private void SaveSettings()
    {
        _mainVm?.SaveTo(_settings!);
        _settingsVm?.SaveTo(_settings!);
        _settings?.Save();
    }

    // ── Global exception handlers ──
    // These catch exceptions that would otherwise crash the app silently or
    // cause the 0xC000041D "unhandled exception in user callback" error.

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogAndShowException("UI Thread Exception", e.Exception);
        e.Handled = true; // Prevent crash
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogAndShowException("Background Thread Exception", ex);
    }

    private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        LogAndShowException("Task Exception", e.Exception);
        e.SetObserved(); // Prevent crash
    }

    private void LogAndShowException(string title, Exception ex)
    {
        var message = $"{ex.GetType().Name}: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";

        // Log to file for debugging
        try
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ModernThinkPadLEDsController",
                "crash.log");

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
            System.IO.File.AppendAllText(logPath,
                $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}\n{message}\n");
        }
        catch { /* Ignore logging errors */ }

        // Show to user
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

}

