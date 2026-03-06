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
    private InpOutDriver?            _driver;
    private DiskActivityMonitor?     _diskMonitor;
    private KeyLockMonitor?          _keyMonitor;
    private KeyboardBacklightMonitor? _kbdMonitor;
    private MicrophoneMuteMonitor?   _micMonitor;
    private PowerEventListener?      _powerListener;
    private TrayIconService?         _tray;
    private AppSettings?             _settings;
    private MainViewModel?           _mainVm;
    private SettingsViewModel?       _settingsVm;
    private MainWindow?              _mainWindow;

    // A named Mutex prevents two instances of the app running simultaneously.
    private System.Threading.Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // --- Single instance guard ---
        _singleInstanceMutex = new System.Threading.Mutex(
            initiallyOwned: true,
            name: "ModernThinkPadLEDsController_SingleInstance",
            out bool isFirstInstance);

        if (!isFirstInstance)
        {
            MessageBox.Show("Modern ThinkPad LEDs Controller is already running.\n\nLook for its icon in the system tray.",
                            "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // --- Open InpOut driver ---
        // TryOpen() calls IsInpOutDriverOpen() which self-installs the kernel
        // service if it isn't running yet. Requires admin rights (manifest).
        if (!InpOutDriver.TryOpen(out _driver))
        {
            var setup = new DriverSetupWindow();
            setup.ShowDialog();

            if (!setup.DriverReady)
            {
                Shutdown();
                return;
            }

            // Try again after the user clicked Initialise in DriverSetupWindow.
            if (!InpOutDriver.TryOpen(out _driver))
            {
                MessageBox.Show("Could not open InpOut driver. Exiting.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
        }

        // --- Hardware layer ---
        var ec   = new EcController(_driver!);
        var leds = new LedController(ec);

        // --- Settings ---
        _settings = AppSettings.Load();

        // --- Monitoring ---
        _diskMonitor   = new DiskActivityMonitor(_settings.HddPollIntervalMs);
        _keyMonitor    = new KeyLockMonitor();
        _kbdMonitor    = new KeyboardBacklightMonitor(leds);
        _micMonitor    = new MicrophoneMuteMonitor();
        _powerListener = new PowerEventListener();

        bool diskOk = _diskMonitor.TryInitialize();
        if (!diskOk)
            _settings.DisableDiskMonitoring = true;

        // --- ViewModels ---
        _mainVm     = new MainViewModel(leds);
        _settingsVm = new SettingsViewModel(_settings, _diskMonitor, _keyMonitor, _kbdMonitor, _powerListener, leds);

        _mainVm.LoadFrom(_settings);
        _settingsVm.LoadFrom(_settings);
        _mainVm.DiskMonitoringAvailable = diskOk;

        // --- Tray icon ---
        _tray = new TrayIconService();
        _tray.Initialize();
        _tray.ShowWindowRequested += ShowMainWindow;
        _tray.ExitRequested       += RequestExit;

        // --- Main window ---
        _mainWindow = new MainWindow(_mainVm, _settingsVm);

        // PowerEventListener needs the window's HWND — only available after
        // the window is created (but before it's shown).
        _mainWindow.SourceInitialized += (_, _) =>
        {
            _powerListener.Attach(_mainWindow);
        };

        // --- Wire monitoring events → ViewModel methods ---
        // All event handlers Dispatcher.Invoke back to the UI thread before
        // touching ViewModel properties, because monitors run on background threads
        // and WPF requires property changes to happen on the UI thread.

        _diskMonitor.StateChanged += state =>
            Dispatcher.Invoke(() =>
            {
                _tray.SetDiskState(state);
                _mainVm.OnDiskStateChanged(state);
            });

        _keyMonitor.CapsLockChanged += isOn =>
            Dispatcher.Invoke(() => _mainVm.OnCapsLockChanged(isOn));

        _keyMonitor.NumLockChanged += isOn =>
            Dispatcher.Invoke(() => _mainVm.OnNumLockChanged(isOn));

        _micMonitor.MuteStateChanged += isMuted =>
            Dispatcher.Invoke(() => _mainVm.OnMicrophoneMuteChanged(isMuted));

        _powerListener.SystemSuspending += () =>
            Dispatcher.Invoke(() =>
            {
                _kbdMonitor.Stop();
                _diskMonitor.Stop();
            });

        _powerListener.SystemResumed += () =>
            Dispatcher.Invoke(() =>
            {
                if (_settings.RememberKeyboardBacklight)
                    _kbdMonitor.RestoreMostCommonLevel();

                _diskMonitor.Start();
                _kbdMonitor.Start();
                _keyMonitor.SyncInitialState();
            });

        _powerListener.LidStateChanged += isOpen =>
            Dispatcher.Invoke(() =>
            {
                if (isOpen && _settings.RememberKeyboardBacklight)
                    _kbdMonitor.RestoreMostCommonLevel();
            });

        _powerListener.FullscreenChanged += isFullscreen =>
            Dispatcher.Invoke(() =>
                _mainVm.OnFullscreenChanged(isFullscreen, _kbdMonitor.CurrentLevel));

        // --- Start monitors ---
        if (!_settings.DisableDiskMonitoring) _diskMonitor.Start();
        if (!_settings.DisableKeyMonitoring)  _keyMonitor.Start();
        if (_settings.RememberKeyboardBacklight) _kbdMonitor.Start();
        if (_settings.DimLedsWhenFullscreen)  _powerListener.StartFullscreenPolling();

        _micMonitor.Start();
        _keyMonitor.SyncInitialState();
        _mainVm.OnMicrophoneMuteChanged(_micMonitor.QueryMuted());

        // --- Show or hide on startup ---
        bool startMinimized = e.Args.Contains("--minimized");
        if (!startMinimized)
            _mainWindow.Show();
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
        _keyMonitor?.Dispose();
        _kbdMonitor?.Dispose();
        _micMonitor?.Dispose();
        _powerListener?.Dispose();
        _tray?.Dispose();
        _driver?.Dispose();

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        Shutdown();
    }
}

