using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace ModernThinkPadLEDsController.Services;

public sealed class ShellCoordinator : IDisposable
{
    private readonly SettingsCoordinator _settingsCoordinator;
    private readonly MainUiController _ui;
    private readonly MonitoringHub _monitoring;
    private readonly TrayIconService _tray;
    private readonly HotkeyService _hotkey;
    private readonly LedBehaviorService _ledBehavior;
    private readonly ILogger<ShellCoordinator> _logger;

    private bool _isInitialized;

    public event Action? ExitRequested;

    public ShellCoordinator(
        SettingsCoordinator settingsCoordinator,
        MainUiController ui,
        MonitoringHub monitoring,
        TrayIconService tray,
        HotkeyService hotkey,
        LedBehaviorService ledBehavior,
        ILogger<ShellCoordinator> logger)
    {
        _settingsCoordinator = settingsCoordinator;
        _ui = ui;
        _monitoring = monitoring;
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
        _ui.SourceInitialized += OnMainWindowSourceInitialized;

        _isInitialized = true;
        _logger.LogInformation("System tray icon configured");
    }

    public void EnsureMainWindowHandle()
    {
        IntPtr handle = _ui.EnsureMainWindowHandle();
        _logger.LogDebug("Main window handle ensured: {Handle}", handle);
    }

    public void Start(bool startMinimized)
    {
        _logger.LogInformation("Start minimized: {StartMinimized}", startMinimized);

        if (!startMinimized)
        {
            _ui.ShowMainWindow();
            _logger.LogInformation("Main window shown");
        }
        else
        {
            _logger.LogInformation("Started minimized to system tray");
        }
    }

    public bool UpdateHotkey(int modifiers, int virtualKey, string displayText)
    {
        _logger.LogInformation("Updating hotkey to {Display} (modifiers={Modifiers:X}, vk={VirtualKey:X})",
            displayText, modifiers, virtualKey);

        bool success = _hotkey.UpdateHotkey(modifiers, virtualKey);

        if (success)
        {
            _settingsCoordinator.UpdateHotkey(modifiers, virtualKey);
        }
        else
        {
            _logger.LogWarning("Failed to register hotkey {Display} - already in use", displayText);
        }

        return success;
    }

    public string GetHotkeyDisplayText()
    {
        return _ui.FormatHotkeyDisplay(_settingsCoordinator.HotkeyModifiers, _settingsCoordinator.HotkeyVirtualKey);
    }

    public void Dispose()
    {
        if (!_isInitialized)
            return;

        _ui.SourceInitialized -= OnMainWindowSourceInitialized;
        _tray.ShowWindowRequested -= OnShowWindowRequested;
        _tray.ExitRequested -= OnExitRequested;
        _hotkey.HotkeyPressed -= OnHotkeyPressed;
        _isInitialized = false;
    }

    private void OnShowWindowRequested()
    {
        _logger.LogDebug("ShowMainWindow requested");
        _ui.ShowAndActivateMainWindow();
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
        _monitoring.PowerListener.Attach(window);
        bool hotkeySuccess = _hotkey.Register(window, _settingsCoordinator.HotkeyModifiers, _settingsCoordinator.HotkeyVirtualKey);
        _hotkey.HotkeyPressed -= OnHotkeyPressed;
        _hotkey.HotkeyPressed += OnHotkeyPressed;

        if (hotkeySuccess)
        {
            _logger.LogInformation("Hotkey service initialized with {Modifiers:X} + {VirtualKey:X}",
                _settingsCoordinator.HotkeyModifiers, _settingsCoordinator.HotkeyVirtualKey);
        }
        else
        {
            _logger.LogWarning("Hotkey {Modifiers:X} + {VirtualKey:X} is already in use - hotkey disabled",
                _settingsCoordinator.HotkeyModifiers, _settingsCoordinator.HotkeyVirtualKey);
        }
    }

    private void OnHotkeyPressed()
    {
        _ui.Dispatch(() => _ledBehavior.OnHotkeyPressed());
    }
}
