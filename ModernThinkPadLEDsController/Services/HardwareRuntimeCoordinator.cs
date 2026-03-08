using Microsoft.Extensions.Logging;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Monitoring;

namespace ModernThinkPadLEDsController.Services;

public sealed class HardwareRuntimeCoordinator : IDisposable
{
    private readonly AppSettings _settings;
    private readonly HardwareAccessController _hardwareAccess;
    private readonly MainUiController _ui;
    private readonly MonitoringHub _monitoring;
    private readonly LedBehaviorService _ledBehavior;
    private readonly ILogger<HardwareRuntimeCoordinator> _logger;

    private bool _eventsWired;

    public HardwareRuntimeCoordinator(
        AppSettings settings,
        HardwareAccessController hardwareAccess,
        MainUiController ui,
        MonitoringHub monitoring,
        LedBehaviorService ledBehavior,
        ILogger<HardwareRuntimeCoordinator> logger)
    {
        _settings = settings;
        _hardwareAccess = hardwareAccess;
        _ui = ui;
        _monitoring = monitoring;
        _ledBehavior = ledBehavior;
        _logger = logger;
    }

    public bool DiskMonitoringAvailable => _monitoring.DiskMonitor.IsAvailable;

    public void Initialize()
    {
        if (_eventsWired)
            return;

        _logger.LogDebug("Wiring up event handlers");

        _ui.DiskModeLedsChanged += OnDiskModeLedsChanged;
        _monitoring.DiskMonitor.StateChanged += OnDiskStateChanged;
        _monitoring.MicrophoneMuteMonitor.MuteStateChanged += OnMicrophoneMuteStateChanged;
        _monitoring.SpeakerMuteMonitor.MuteStateChanged += OnSpeakerMuteStateChanged;
        _monitoring.PowerListener.SystemSuspending += OnSystemSuspending;
        _monitoring.PowerListener.SystemResumed += OnSystemResumed;
        _monitoring.PowerListener.LidStateChanged += OnLidStateChanged;
        _monitoring.PowerListener.FullscreenChanged += OnFullscreenChanged;

        _eventsWired = true;
        _logger.LogInformation("Event handlers wired up successfully");
    }

    public void StartInitialDiskMonitoring()
    {
        if (_hardwareAccess.IsEnabled && _ui.HasDiskModeLeds && DiskMonitoringAvailable)
        {
            _monitoring.DiskMonitor.Start();
            _logger.LogInformation("Disk monitoring started (has disk mode LEDs)");
        }
        else if (!DiskMonitoringAvailable)
        {
            _logger.LogWarning("Disk performance counters are unavailable - disk LED modes are disabled");
        }
    }

    public void StartMonitors()
    {
        _logger.LogInformation("Starting monitors");

        if (_hardwareAccess.IsEnabled && (_settings.RememberKeyboardBacklight || _settings.DimLedsWhenFullscreen))
        {
            _monitoring.KeyboardBacklightMonitor.Start();
            _logger.LogDebug("Keyboard backlight monitor started");
        }

        if (_hardwareAccess.IsEnabled && _settings.DimLedsWhenFullscreen)
        {
            _monitoring.PowerListener.StartFullscreenPolling();
            _logger.LogDebug("Fullscreen polling started");
        }

        _monitoring.MicrophoneMuteMonitor.Start();
        _logger.LogDebug("Microphone mute monitor started");

        bool microphoneMuted = _monitoring.MicrophoneMuteMonitor.QueryMuted();
        _ledBehavior.ObserveMicrophoneMuteState(microphoneMuted);
        _logger.LogDebug("Initial microphone mute state: {IsMuted}", microphoneMuted);

        _monitoring.SpeakerMuteMonitor.Start();
        _logger.LogDebug("Speaker mute monitor started");

        bool speakerMuted = _monitoring.SpeakerMuteMonitor.QueryMuted();
        _ledBehavior.ObserveSpeakerMuteState(speakerMuted);
        _logger.LogDebug("Initial speaker mute state: {IsMuted}", speakerMuted);
    }

    public void Dispose()
    {
        if (!_eventsWired)
            return;

        _ui.DiskModeLedsChanged -= OnDiskModeLedsChanged;
        _monitoring.DiskMonitor.StateChanged -= OnDiskStateChanged;
        _monitoring.MicrophoneMuteMonitor.MuteStateChanged -= OnMicrophoneMuteStateChanged;
        _monitoring.SpeakerMuteMonitor.MuteStateChanged -= OnSpeakerMuteStateChanged;
        _monitoring.PowerListener.SystemSuspending -= OnSystemSuspending;
        _monitoring.PowerListener.SystemResumed -= OnSystemResumed;
        _monitoring.PowerListener.LidStateChanged -= OnLidStateChanged;
        _monitoring.PowerListener.FullscreenChanged -= OnFullscreenChanged;
        _eventsWired = false;
    }

    private void OnDiskModeLedsChanged(bool hasDiskModes)
    {
        _ui.Dispatch(() =>
        {
            _logger.LogDebug("Disk mode LEDs changed: {HasDiskModes}", hasDiskModes);

            if (!_hardwareAccess.IsEnabled)
            {
                _monitoring.DiskMonitor.Stop();
                return;
            }

            if (hasDiskModes && _monitoring.DiskMonitor.IsAvailable)
                _monitoring.DiskMonitor.Start();
            else
                _monitoring.DiskMonitor.Stop();
        });
    }

    private void OnDiskStateChanged(DiskActivityState state)
    {
        if (!_hardwareAccess.IsEnabled)
            return;

        _ui.Dispatch(() =>
        {
            _logger.LogDebug("Disk state changed: {State}", state);
            _ledBehavior.OnDiskStateChanged(state);
        });
    }

    private void OnMicrophoneMuteStateChanged(bool isMuted)
    {
        _ui.Dispatch(() =>
        {
            _logger.LogDebug("Microphone mute state changed: {IsMuted}", isMuted);
            _ledBehavior.ObserveMicrophoneMuteState(isMuted);
        });
    }

    private void OnSpeakerMuteStateChanged(bool isMuted)
    {
        _ui.Dispatch(() =>
        {
            _logger.LogDebug("Speaker mute state changed: {IsMuted}", isMuted);
            _ledBehavior.ObserveSpeakerMuteState(isMuted);
        });
    }

    private void OnSystemSuspending()
    {
        _ui.Dispatch(() =>
        {
            _logger.LogInformation("System suspending - stopping monitors");
            _monitoring.KeyboardBacklightMonitor.Stop();
            _monitoring.DiskMonitor.Stop();
        });
    }

    private void OnSystemResumed()
    {
        _ui.Dispatch(() =>
        {
            _logger.LogInformation("System resumed - restarting monitors");
            if (!_hardwareAccess.IsEnabled)
                return;

            if (_settings.RememberKeyboardBacklight && _settings.SavedKeyboardBacklight is int savedKeyboardBacklight)
                _ui.SetKeyboardBrightnessLevel(savedKeyboardBacklight);

            if (_ui.HasDiskModeLeds)
                _monitoring.DiskMonitor.Start();

            _monitoring.KeyboardBacklightMonitor.Start();
        });
    }

    private void OnLidStateChanged(bool isOpen)
    {
        if (!_hardwareAccess.IsEnabled)
            return;

        _ui.Dispatch(() =>
        {
            _logger.LogDebug("Lid state changed: {IsOpen}", isOpen);
            if (isOpen && _settings.RememberKeyboardBacklight && _settings.SavedKeyboardBacklight is int savedKeyboardBacklight)
                _ui.SetKeyboardBrightnessLevel(savedKeyboardBacklight);
        });
    }

    private void OnFullscreenChanged(bool isFullscreen)
    {
        if (!_hardwareAccess.IsEnabled)
            return;

        _ui.Dispatch(() =>
        {
            _logger.LogDebug("Fullscreen state changed: {IsFullscreen}", isFullscreen);

            if (!_monitoring.KeyboardBacklightMonitor.HasObservedLevel)
                return;

            byte currentLevel = _monitoring.KeyboardBacklightMonitor.CurrentLevel;
            _ledBehavior.OnFullscreenChanged(isFullscreen, currentLevel);
        });
    }
}
