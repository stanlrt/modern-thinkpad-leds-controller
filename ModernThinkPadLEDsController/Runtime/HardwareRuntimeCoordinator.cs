using Microsoft.Extensions.Logging;
using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.Monitoring;
using ModernThinkPadLEDsController.Presentation.Services;
using ModernThinkPadLEDsController.Shell;

namespace ModernThinkPadLEDsController.Runtime;

/// <summary>
/// Coordinates monitor events with runtime LED behavior.
/// </summary>
public sealed class HardwareRuntimeCoordinator : IDisposable
{
    private readonly AppSettings _settings;
    private readonly HardwareAccessController _hardwareAccess;
    private readonly MainPresentationService _presentation;
    private readonly MainWindowHost _windowHost;
    private readonly DiskActivityMonitor _diskMonitor;
    private readonly KeyboardBacklightMonitor _keyboardBacklightMonitor;
    private readonly MicrophoneMuteMonitor _microphoneMuteMonitor;
    private readonly SpeakerMuteMonitor _speakerMuteMonitor;
    private readonly PowerEventMonitor _powerEventMonitor;
    private readonly FullscreenMonitor _fullscreenMonitor;
    private readonly LedBehaviorService _ledBehavior;
    private readonly ILogger<HardwareRuntimeCoordinator> _logger;

    private bool _eventsWired;

    public HardwareRuntimeCoordinator(
        AppSettings settings,
        HardwareAccessController hardwareAccess,
        MainPresentationService presentation,
        MainWindowHost windowHost,
        DiskActivityMonitor diskMonitor,
        KeyboardBacklightMonitor keyboardBacklightMonitor,
        MicrophoneMuteMonitor microphoneMuteMonitor,
        SpeakerMuteMonitor speakerMuteMonitor,
        PowerEventMonitor powerEventMonitor,
        FullscreenMonitor fullscreenMonitor,
        LedBehaviorService ledBehavior,
        ILogger<HardwareRuntimeCoordinator> logger)
    {
        _settings = settings;
        _hardwareAccess = hardwareAccess;
        _presentation = presentation;
        _windowHost = windowHost;
        _diskMonitor = diskMonitor;
        _keyboardBacklightMonitor = keyboardBacklightMonitor;
        _microphoneMuteMonitor = microphoneMuteMonitor;
        _speakerMuteMonitor = speakerMuteMonitor;
        _powerEventMonitor = powerEventMonitor;
        _fullscreenMonitor = fullscreenMonitor;
        _ledBehavior = ledBehavior;
        _logger = logger;
    }

    public bool DiskMonitoringAvailable => _diskMonitor.IsAvailable;

    public void Initialize()
    {
        if (_eventsWired)
        {
            return;
        }

        _logger.LogDebug("Wiring up event handlers");

        _presentation.DiskModeLedsChanged += OnDiskModeLedsChanged;
        _diskMonitor.StateChanged += OnDiskStateChanged;
        _microphoneMuteMonitor.MuteStateChanged += OnMicrophoneMuteStateChanged;
        _speakerMuteMonitor.MuteStateChanged += OnSpeakerMuteStateChanged;
        _powerEventMonitor.SystemSuspending += OnSystemSuspending;
        _powerEventMonitor.SystemResumed += OnSystemResumed;
        _powerEventMonitor.LidStateChanged += OnLidStateChanged;
        _fullscreenMonitor.FullscreenChanged += OnFullscreenChanged;

        _eventsWired = true;
        _logger.LogInformation("Event handlers wired up successfully");
    }

    public void StartInitialDiskMonitoring()
    {
        if (_hardwareAccess.IsEnabled && _presentation.HasDiskModeLeds && DiskMonitoringAvailable)
        {
            _diskMonitor.Start();
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
            _keyboardBacklightMonitor.Start();
            _logger.LogDebug("Keyboard backlight monitor started");
        }

        if (_hardwareAccess.IsEnabled && _settings.DimLedsWhenFullscreen)
        {
            _fullscreenMonitor.Start();
            _logger.LogDebug("Fullscreen polling started");
        }

        StartMonitor(_microphoneMuteMonitor);
        _logger.LogDebug("Microphone mute monitor started");

        bool microphoneMuted = _microphoneMuteMonitor.QueryMuted();
        _ledBehavior.ObserveMicrophoneMuteState(microphoneMuted);
        _logger.LogDebug("Initial microphone mute state: {IsMuted}", microphoneMuted);

        StartMonitor(_speakerMuteMonitor);
        _logger.LogDebug("Speaker mute monitor started");

        bool speakerMuted = _speakerMuteMonitor.QueryMuted();
        _ledBehavior.ObserveSpeakerMuteState(speakerMuted);
        _logger.LogDebug("Initial speaker mute state: {IsMuted}", speakerMuted);
    }

    public void Dispose()
    {
        if (!_eventsWired)
        {
            return;
        }

        _presentation.DiskModeLedsChanged -= OnDiskModeLedsChanged;
        _diskMonitor.StateChanged -= OnDiskStateChanged;
        _microphoneMuteMonitor.MuteStateChanged -= OnMicrophoneMuteStateChanged;
        _speakerMuteMonitor.MuteStateChanged -= OnSpeakerMuteStateChanged;
        _powerEventMonitor.SystemSuspending -= OnSystemSuspending;
        _powerEventMonitor.SystemResumed -= OnSystemResumed;
        _powerEventMonitor.LidStateChanged -= OnLidStateChanged;
        _fullscreenMonitor.FullscreenChanged -= OnFullscreenChanged;
        _eventsWired = false;
    }

    private void OnDiskModeLedsChanged(bool hasDiskModes)
    {
        _windowHost.Dispatch(() =>
        {
            _logger.LogDebug("Disk mode LEDs changed: {HasDiskModes}", hasDiskModes);

            if (!_hardwareAccess.IsEnabled)
            {
                _diskMonitor.Stop();
                return;
            }

            if (hasDiskModes && _diskMonitor.IsAvailable)
            {
                _diskMonitor.Start();
            }
            else
            {
                _diskMonitor.Stop();
            }
        });
    }

    private void OnDiskStateChanged(DiskActivityState state)
    {
        if (!_hardwareAccess.IsEnabled)
        {
            return;
        }

        _windowHost.Dispatch(() =>
        {
            _logger.LogDebug("Disk state changed: {State}", state);
            _ledBehavior.OnDiskStateChanged(state);
        });
    }

    private void OnMicrophoneMuteStateChanged(bool isMuted)
    {
        _windowHost.Dispatch(() =>
        {
            _logger.LogDebug("Microphone mute state changed: {IsMuted}", isMuted);
            _ledBehavior.ObserveMicrophoneMuteState(isMuted);
        });
    }

    private void OnSpeakerMuteStateChanged(bool isMuted)
    {
        _windowHost.Dispatch(() =>
        {
            _logger.LogDebug("Speaker mute state changed: {IsMuted}", isMuted);
            _ledBehavior.ObserveSpeakerMuteState(isMuted);
        });
    }

    private void OnSystemSuspending()
    {
        _windowHost.Dispatch(() =>
        {
            _logger.LogInformation("System suspending - stopping monitors");
            StopMonitors(_keyboardBacklightMonitor, _diskMonitor, _fullscreenMonitor);
        });
    }

    private void OnSystemResumed()
    {
        _windowHost.Dispatch(() =>
        {
            _logger.LogInformation("System resumed - restarting monitors");
            if (!_hardwareAccess.IsEnabled)
            {
                return;
            }

            if (_settings.RememberKeyboardBacklight && _settings.SavedKeyboardBacklight is int savedKeyboardBacklight)
            {
                _presentation.SetKeyboardBrightnessLevel(savedKeyboardBacklight);
            }

            if (_presentation.HasDiskModeLeds)
            {
                StartMonitor(_diskMonitor);
            }

            StartMonitor(_keyboardBacklightMonitor);

            if (_settings.DimLedsWhenFullscreen)
            {
                StartMonitor(_fullscreenMonitor);
            }
        });
    }

    private static void StartMonitor(ILifecycleMonitor monitor)
    {
        monitor.Start();
    }

    private static void StopMonitors(params ILifecycleMonitor[] monitors)
    {
        foreach (ILifecycleMonitor monitor in monitors)
        {
            monitor.Stop();
        }
    }

    private void OnLidStateChanged(bool isOpen)
    {
        if (!_hardwareAccess.IsEnabled)
        {
            return;
        }

        _windowHost.Dispatch(() =>
        {
            _logger.LogDebug("Lid state changed: {IsOpen}", isOpen);
            if (isOpen && _settings.RememberKeyboardBacklight && _settings.SavedKeyboardBacklight is int savedKeyboardBacklight)
            {
                _presentation.SetKeyboardBrightnessLevel(savedKeyboardBacklight);
            }
        });
    }

    private void OnFullscreenChanged(bool isFullscreen)
    {
        if (!_hardwareAccess.IsEnabled)
        {
            return;
        }

        _windowHost.Dispatch(() =>
        {
            _logger.LogDebug("Fullscreen state changed: {IsFullscreen}", isFullscreen);

            if (!_keyboardBacklightMonitor.HasObservedLevel)
            {
                return;
            }

            byte currentLevel = _keyboardBacklightMonitor.CurrentLevel;
            _ledBehavior.OnFullscreenChanged(isFullscreen, currentLevel);
        });
    }
}
