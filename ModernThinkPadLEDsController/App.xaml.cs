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
    private InpOutDriver? _driver;
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
        if (!InpOutDriver.TryOpen(out _driver))
        {
            var setup = new DriverSetupWindow();
            setup.ShowDialog();

            if (!setup.DriverReady)
            {
                Shutdown();
                return false;
            }

            if (!InpOutDriver.TryOpen(out _driver))
            {
                System.Windows.MessageBox.Show("Could not open InpOut driver. Exiting.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return false;
            }
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
        _mainVm = new MainViewModel(leds);
        _settingsVm = new SettingsViewModel(_settings!, _diskMonitor!, _kbdMonitor!, _powerListener!, leds);

        _mainVm.LoadFrom(_settings!);
        _settingsVm.LoadFrom(_settings!);
        _mainVm.DiskMonitoringAvailable = diskOk;
        _mainVm.ApplyAll();

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
        _mainVm?.SaveTo(_settings!);
        _settingsVm?.SaveTo(_settings!);
        _settings?.Save();

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

}

