using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.Monitoring;
using ModernThinkPadLEDsController.Presentation.Views;
using ModernThinkPadLEDsController.Settings;

namespace ModernThinkPadLEDsController.Shell;

/// <summary>
/// Coordinates shell-facing behavior around the main window and tray icon.
/// </summary>
public sealed class ShellCoordinator : IDisposable
{
    private readonly SettingsPersistenceService _settingsPersistence;
    private readonly MainWindowHost _windowHost;
    private readonly PowerEventMonitor _powerEventMonitor;
    private readonly FullscreenMonitor _fullscreenMonitor;
    private readonly TrayIconService _tray;
    private readonly HotkeyService _hotkey;
    private readonly LedBehaviorService _ledBehavior;
    private readonly ILogger<ShellCoordinator> _logger;

    private bool _isInitialized;

    public event Action? ExitRequested;

    public ShellCoordinator(
        SettingsPersistenceService settingsPersistence,
        MainWindowHost windowHost,
        PowerEventMonitor powerEventMonitor,
        FullscreenMonitor fullscreenMonitor,
        TrayIconService tray,
        HotkeyService hotkey,
        LedBehaviorService ledBehavior,
        ILogger<ShellCoordinator> logger)
    {
        _settingsPersistence = settingsPersistence;
        _windowHost = windowHost;
        _powerEventMonitor = powerEventMonitor;
        _fullscreenMonitor = fullscreenMonitor;
        _tray = tray;
        _hotkey = hotkey;
        _ledBehavior = ledBehavior;
        _logger = logger;
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        _tray.ShowWindowRequested += OnShowWindowRequested;
        _tray.ExitRequested += OnExitRequested;
        _windowHost.SourceInitialized += OnMainWindowSourceInitialized;

        _isInitialized = true;
        _logger.LogInformation("System tray icon configured");
    }

    public void EnsureMainWindowHandle()
    {
        IntPtr handle = _windowHost.EnsureMainWindowHandle();
        _logger.LogDebug("Main window handle ensured: {Handle}", handle);
    }

    public void Start(bool startMinimized)
    {
        _logger.LogInformation("Start minimized: {StartMinimized}", startMinimized);

        if (!startMinimized)
        {
            _windowHost.ShowMainWindow();
            _logger.LogInformation("Main window shown");
        }
        else
        {
            _logger.LogInformation("Started minimized to system tray");
        }
    }

    public bool UpdateHotkey(HotkeyModifiers modifiers, Key key, string displayText)
    {
        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        _logger.LogInformation("Updating hotkey to {Display} (modifiers={Modifiers:X}, vk={VirtualKey:X})",
            displayText, (int)modifiers, virtualKey);

        bool success = _hotkey.UpdateHotkey(modifiers, key);

        if (success)
        {
            _settingsPersistence.UpdateHotkey(modifiers, key);
        }
        else
        {
            _logger.LogWarning("Failed to register hotkey {Display} - already in use", displayText);
        }

        return success;
    }

    public void Dispose()
    {
        if (!_isInitialized)
            return;

        _windowHost.SourceInitialized -= OnMainWindowSourceInitialized;
        _tray.ShowWindowRequested -= OnShowWindowRequested;
        _tray.ExitRequested -= OnExitRequested;
        _hotkey.HotkeyPressed -= OnHotkeyPressed;
        _isInitialized = false;
    }

    private void OnShowWindowRequested()
    {
        _logger.LogDebug("ShowMainWindow requested");
        _windowHost.ShowAndActivateMainWindow();
        _logger.LogInformation("Main window shown and activated");
    }

    private void OnExitRequested()
    {
        ExitRequested?.Invoke();
    }

    private void OnMainWindowSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        _logger.LogDebug("Main window source initialized");
        _powerEventMonitor.Attach(window);
        _fullscreenMonitor.Attach(window);
        bool hotkeySuccess = _hotkey.Register(window, _settingsPersistence.HotkeyModifiers, _settingsPersistence.HotkeyKey);
        _hotkey.HotkeyPressed -= OnHotkeyPressed;
        _hotkey.HotkeyPressed += OnHotkeyPressed;

        int configuredVirtualKey = KeyInterop.VirtualKeyFromKey(_settingsPersistence.HotkeyKey);

        if (hotkeySuccess)
        {
            _logger.LogInformation("Hotkey service initialized with {Modifiers:X} + {VirtualKey:X}",
                (int)_settingsPersistence.HotkeyModifiers, configuredVirtualKey);
        }
        else
        {
            _logger.LogWarning("Hotkey {Modifiers:X} + {VirtualKey:X} is already in use - hotkey disabled",
                (int)_settingsPersistence.HotkeyModifiers, configuredVirtualKey);
        }
    }

    private void OnHotkeyPressed()
    {
        _windowHost.Dispatch(() => _ledBehavior.OnHotkeyPressed());
    }
}
