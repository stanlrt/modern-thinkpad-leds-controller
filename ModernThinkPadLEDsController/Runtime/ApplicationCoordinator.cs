using Microsoft.Extensions.Logging;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.Presentation.Services;
using ModernThinkPadLEDsController.Settings;
using ModernThinkPadLEDsController.Shell;

namespace ModernThinkPadLEDsController.Runtime;

/// <summary>
/// Orchestrates startup across presentation, runtime, shell, and settings.
/// </summary>
public sealed class ApplicationCoordinator : IDisposable
{
    private readonly HardwareAccessController _hardwareAccess;
    private readonly MainPresentationService _presentation;
    private readonly ShellCoordinator _shell;
    private readonly HardwareRuntimeCoordinator _runtime;
    private readonly SettingsPersistenceService _settingsPersistence;
    private readonly LedBehaviorService _ledBehavior;
    private readonly ILogger<ApplicationCoordinator> _logger;

    private bool _isInitialized;

    public event Action? ExitRequested;

    public ApplicationCoordinator(
        HardwareAccessController hardwareAccess,
        MainPresentationService presentation,
        ShellCoordinator shell,
        HardwareRuntimeCoordinator runtime,
        SettingsPersistenceService settingsPersistence,
        LedBehaviorService ledBehavior,
        ILogger<ApplicationCoordinator> logger)
    {
        _hardwareAccess = hardwareAccess;
        _presentation = presentation;
        _shell = shell;
        _runtime = runtime;
        _settingsPersistence = settingsPersistence;
        _ledBehavior = ledBehavior;
        _logger = logger;
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        _logger.LogInformation("Initializing services and UI");

        bool diskMonitoringAvailable = _runtime.DiskMonitoringAvailable;

        _presentation.LoadFromSettings();
        _logger.LogDebug("Settings loaded into view models");

        _presentation.SetDiskMonitoringAvailable(diskMonitoringAvailable);

        if (_hardwareAccess.IsEnabled)
        {
            _ledBehavior.ApplyAll();
            _logger.LogInformation("LED configurations applied");
        }
        else
        {
            _logger.LogWarning("Hardware access is disabled - skipping initial LED application");
        }

        _presentation.SetSaveCallbacks(_settingsPersistence.SaveCurrentSettings);
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
