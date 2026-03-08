using Microsoft.Extensions.Logging;
using ModernThinkPadLEDsController.Hardware;

namespace ModernThinkPadLEDsController.Services;

public sealed class ApplicationCoordinator : IDisposable
{
    private readonly HardwareAccessController _hardwareAccess;
    private readonly MainUiController _ui;
    private readonly ShellCoordinator _shell;
    private readonly HardwareRuntimeCoordinator _runtime;
    private readonly SettingsCoordinator _settingsCoordinator;
    private readonly LedBehaviorService _ledBehavior;
    private readonly ILogger<ApplicationCoordinator> _logger;

    private bool _isInitialized;

    public event Action? ExitRequested;

    public ApplicationCoordinator(
        HardwareAccessController hardwareAccess,
        MainUiController ui,
        ShellCoordinator shell,
        HardwareRuntimeCoordinator runtime,
        SettingsCoordinator settingsCoordinator,
        LedBehaviorService ledBehavior,
        ILogger<ApplicationCoordinator> logger)
    {
        _hardwareAccess = hardwareAccess;
        _ui = ui;
        _shell = shell;
        _runtime = runtime;
        _settingsCoordinator = settingsCoordinator;
        _ledBehavior = ledBehavior;
        _logger = logger;
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        _logger.LogInformation("Initializing services and UI");

        bool diskMonitoringAvailable = _runtime.DiskMonitoringAvailable;

        _ui.LoadFromSettings();
        _logger.LogDebug("Settings loaded into view models");

        _ui.SetDiskMonitoringAvailable(diskMonitoringAvailable);

        if (_hardwareAccess.IsEnabled)
        {
            _ledBehavior.ApplyAll();
            _logger.LogInformation("LED configurations applied");
        }
        else
        {
            _logger.LogWarning("Hardware access is disabled - skipping initial LED application");
        }

        _ui.SetSaveCallbacks(_settingsCoordinator.SaveCurrentSettings);
        _logger.LogDebug("Save callbacks configured");

        _shell.Initialize();
        _shell.ExitRequested += OnExitRequested;

        _runtime.Initialize();

        _runtime.StartInitialDiskMonitoring();

        _isInitialized = true;
    }

    public void EnsureMainWindowHandle()
    {
        _shell.EnsureMainWindowHandle();
    }

    public void Start(bool startMinimized)
    {
        _runtime.StartMonitors();
        _shell.Start(startMinimized);
    }

    public void Dispose()
    {
        _shell.ExitRequested -= OnExitRequested;
        _runtime.Dispose();
        _shell.Dispose();
    }

    private void OnExitRequested()
    {
        ExitRequested?.Invoke();
    }
}
