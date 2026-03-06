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

        // --- Single instance guard ---
        _singleInstanceMutex = new System.Threading.Mutex(
            initiallyOwned: true,
            name: "ModernThinkPadLEDsController_SingleInstance",
            out bool isFirstInstance);

        if (!isFirstInstance)
        {
            System.Windows.MessageBox.Show("Modern ThinkPad LEDs Controller is already running.\n\nLook for its icon in the system tray.",
                            "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // --- Open InpOut driver ---
        if (!InpOutDriver.TryOpen(out _driver))
        {
            var setup = new DriverSetupWindow();
            setup.ShowDialog();

            if (!setup.DriverReady)
            {
                Shutdown();
                return;
            }

            if (!InpOutDriver.TryOpen(out _driver))
            {
                System.Windows.MessageBox.Show("Could not open InpOut driver. Exiting.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
        }

        // --- Hardware layer ---
        var ec = new EcController(_driver!);
        var leds = new LedController(ec);

        // --- Settings ---
        _settings = AppSettings.Load();

        // --- Monitoring ---
        _diskMonitor = new DiskActivityMonitor(_settings.HddPollIntervalMs);
        _kbdMonitor = new KeyboardBacklightMonitor(leds);
        _micMonitor = new MicrophoneMuteMonitor();
        _powerListener = new PowerEventListener();

        bool diskOk = _diskMonitor.TryInitialize();

        // --- ViewModels ---
        _mainVm = new MainViewModel(leds);
        _settingsVm = new SettingsViewModel(_settings, _diskMonitor, _kbdMonitor, _powerListener, leds);

        _mainVm.LoadFrom(_settings);
        _settingsVm.LoadFrom(_settings);
        _mainVm.DiskMonitoringAvailable = diskOk;

        // Apply static LED modes (On/Off/Blink) to hardware immediately after load.
        _mainVm.ApplyAll();

        // --- Tray icon ---
        _tray = new TrayIconService();
        _tray.Initialize();
        _tray.ShowWindowRequested += ShowMainWindow;
        _tray.ExitRequested += RequestExit;

        // --- Main window ---
        _mainWindow = new MainWindow(_mainVm, _settingsVm);

        _mainWindow.SourceInitialized += (_, _) =>
        {
            _powerListener.Attach(_mainWindow);

            // Register Win+Shift+K global hotkey — requires a valid HWND.
            _hotkey = new HotkeyService();
            _hotkey.Register(_mainWindow);
            _hotkey.HotkeyPressed += () => Dispatcher.Invoke(() => _mainVm.OnHotkeyPressed());
        };

        // --- Wire monitoring events → ViewModel methods ---
        _diskMonitor.StateChanged += state =>
            Dispatcher.Invoke(() =>
            {
                _tray.SetDiskState(state);
                _mainVm.OnDiskStateChanged(state);
            });

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
        if (diskOk) _diskMonitor.Start();
        if (_settings.RememberKeyboardBacklight) _kbdMonitor.Start();
        if (_settings.DimLedsWhenFullscreen) _powerListener.StartFullscreenPolling();

        _micMonitor.Start();

        // Sync mic LED to current mute state before the first poll fires.
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

    // Falls back to default WPF scrolling if anything goes wrong — no crash.
    private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        try
        {
            // When "scroll one screen at a time" is set in mouse settings, let WPF handle it.
            if (System.Windows.Forms.SystemInformation.MouseWheelScrollLines == -1)
            {
                e.Handled = false;
            }
            else
            {
                try
                {
                    var sv = (System.Windows.Controls.ScrollViewer)sender;
                    sv.ScrollToVerticalOffset(
                        sv.VerticalOffset -
                        e.Delta * 10 * System.Windows.Forms.SystemInformation.MouseWheelScrollLines / (double)120);
                    e.Handled = true;
                }
                catch (Exception)
                {
                    // Intentionally swallowed: fall back to default WPF scrolling.
                }
            }
        }
        catch (Exception)
        {
            // Intentionally swallowed: fall back to default WPF scrolling.
        }
    }
}

