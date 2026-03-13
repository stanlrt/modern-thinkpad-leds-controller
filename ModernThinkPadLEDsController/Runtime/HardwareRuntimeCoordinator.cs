using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly IUiDispatcher _dispatcher;
    private readonly DiskActivityMonitor _diskMonitor;
    private readonly KeyboardBacklightMonitor _keyboardBacklightMonitor;
    private readonly MicrophoneMuteMonitor _microphoneMuteMonitor;
    private readonly SpeakerMuteMonitor _speakerMuteMonitor;
    private readonly PowerEventMonitor _powerEventMonitor;
    private readonly FullscreenMonitor _fullscreenMonitor;
    private readonly LedBehaviorService _ledBehavior;
    private readonly ILogger<HardwareRuntimeCoordinator> _logger;

    private bool _eventsWired;
    private CancellationTokenSource? _ledReapplyCts;

    public HardwareRuntimeCoordinator(
        AppSettings settings,
        HardwareAccessController hardwareAccess,
        MainPresentationService presentation,
        IUiDispatcher dispatcher,
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
        _dispatcher = dispatcher;
        _diskMonitor = diskMonitor;
        _keyboardBacklightMonitor = keyboardBacklightMonitor;
        _microphoneMuteMonitor = microphoneMuteMonitor;
        _speakerMuteMonitor = speakerMuteMonitor;
        _powerEventMonitor = powerEventMonitor;
        _fullscreenMonitor = fullscreenMonitor;
        _ledBehavior = ledBehavior;
        _logger = logger;
    }

    /// <summary>
    /// Internal constructor for testing the reapply tick in isolation.
    /// <para>
    /// <strong>Only <see cref="ExecuteReapplyTick"/> and <see cref="Dispose"/> are safe to call
    /// on instances created with this constructor.</strong> All other methods will throw
    /// <see cref="NullReferenceException"/> because the monitor, presentation, and dispatcher
    /// fields are intentionally left null.
    /// </para>
    /// </summary>
    internal HardwareRuntimeCoordinator(
        HardwareAccessController hardwareAccess,
        LedBehaviorService ledBehavior)
    {
        _hardwareAccess = hardwareAccess;
        _ledBehavior = ledBehavior;
        _logger = NullLogger<HardwareRuntimeCoordinator>.Instance;
        _settings = null!;
        _presentation = null!;
        _dispatcher = null!;
        _diskMonitor = null!;
        _keyboardBacklightMonitor = null!;
        _microphoneMuteMonitor = null!;
        _speakerMuteMonitor = null!;
        _powerEventMonitor = null!;
        _fullscreenMonitor = null!;
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
        _presentation.LedConfigurationChanged += OnLedConfigurationChanged;
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
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Initial microphone mute state: {IsMuted}", microphoneMuted);
        }

        StartMonitor(_speakerMuteMonitor);
        _logger.LogDebug("Speaker mute monitor started");

        bool speakerMuted = _speakerMuteMonitor.QueryMuted();
        _ledBehavior.ObserveSpeakerMuteState(speakerMuted);
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Initial speaker mute state: {IsMuted}", speakerMuted);
        }

        UpdateLedReapplyLoopState();
    }

    public void Dispose()
    {
        if (!_eventsWired)
        {
            return;
        }

        StopLedReapplyLoop();

        _presentation.DiskModeLedsChanged -= OnDiskModeLedsChanged;
        _presentation.LedConfigurationChanged -= OnLedConfigurationChanged;
        _diskMonitor.StateChanged -= OnDiskStateChanged;
        _microphoneMuteMonitor.MuteStateChanged -= OnMicrophoneMuteStateChanged;
        _speakerMuteMonitor.MuteStateChanged -= OnSpeakerMuteStateChanged;
        _powerEventMonitor.SystemSuspending -= OnSystemSuspending;
        _powerEventMonitor.SystemResumed -= OnSystemResumed;
        _powerEventMonitor.LidStateChanged -= OnLidStateChanged;
        _fullscreenMonitor.FullscreenChanged -= OnFullscreenChanged;
        _eventsWired = false;
    }

    /// <summary>
    /// Applies one reapply-loop tick: reasserts managed LED states if hardware is enabled
    /// and any LED needs periodic backstop. Extracted for deterministic testing without
    /// waiting on real Task.Delay timing.
    /// </summary>
    internal void ExecuteReapplyTick()
    {
        if (_hardwareAccess.IsEnabled && _ledBehavior.NeedsPeriodicReapply())
        {
            _ledBehavior.ReapplyManagedStates();
        }

        // Periodically enforce keyboard backlight level if enabled
        // Skip if _settings/_presentation are null (test constructor scenario)
        if (_settings is not null && _presentation is not null &&
            _hardwareAccess.IsEnabled && _settings.EnforceKeyboardBacklight && _settings.SavedKeyboardBacklight is int savedLevel)
        {
            _presentation.SetKeyboardBrightnessLevel(savedLevel, forceWrite: true);
        }
    }

    private void OnDiskModeLedsChanged(bool hasDiskModes)
    {
        _dispatcher.Dispatch(() =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Disk mode LEDs changed: {HasDiskModes}", hasDiskModes);
            }

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

    private void OnLedConfigurationChanged()
    {
        _dispatcher.Dispatch(UpdateLedReapplyLoopState);
    }

    private void OnDiskStateChanged(DiskActivityState state)
    {
        if (!_hardwareAccess.IsEnabled)
        {
            return;
        }

        _dispatcher.Dispatch(() =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Disk state changed: {State}", state);
            }
            _ledBehavior.OnDiskStateChanged(state);
        });
    }

    private void OnMicrophoneMuteStateChanged(bool isMuted)
    {
        _dispatcher.Dispatch(() =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Microphone mute state changed: {IsMuted}", isMuted);
            }
            _ledBehavior.ObserveMicrophoneMuteState(isMuted);
        });
    }

    private void OnSpeakerMuteStateChanged(bool isMuted)
    {
        _dispatcher.Dispatch(() =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Speaker mute state changed: {IsMuted}", isMuted);
            }
            _ledBehavior.ObserveSpeakerMuteState(isMuted);
        });
    }

    private void OnSystemSuspending()
    {
        _dispatcher.Dispatch(() =>
        {
            _logger.LogInformation("System suspending - stopping monitors");
            StopLedReapplyLoop();
            StopMonitors(_keyboardBacklightMonitor, _diskMonitor, _fullscreenMonitor);
        });
    }

    private void OnSystemResumed()
    {
        _dispatcher.Dispatch(() =>
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

            UpdateLedReapplyLoopState();
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

    private void StartLedReapplyLoop()
    {
        if (_ledReapplyCts is not null)
        {
            return;
        }

        _ledReapplyCts = new CancellationTokenSource();
        CancellationToken token = _ledReapplyCts.Token;
        Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int intervalMs = Math.Max(AppSettingsDefaults.MIN_LED_REAPPLY_INTERVAL_MS, _settings.LedReapplyIntervalMs);
                    await Task.Delay(intervalMs, token);

                    _dispatcher.Dispatch(ExecuteReapplyTick);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void StopLedReapplyLoop()
    {
        _ledReapplyCts?.Cancel();
        _ledReapplyCts?.Dispose();
        _ledReapplyCts = null;
    }

    private void UpdateLedReapplyLoopState()
    {
        if (!_hardwareAccess.IsEnabled)
        {
            StopLedReapplyLoop();
            return;
        }

        bool needsReapply = _ledBehavior.NeedsPeriodicReapply() ||
                           (_settings.EnforceKeyboardBacklight && _settings.SavedKeyboardBacklight.HasValue);

        if (needsReapply)
        {
            StartLedReapplyLoop();
        }
        else
        {
            StopLedReapplyLoop();
        }
    }

    private void OnLidStateChanged(bool isOpen)
    {
        if (!_hardwareAccess.IsEnabled)
        {
            return;
        }

        _dispatcher.Dispatch(() =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Lid state changed: {IsOpen}", isOpen);
            }
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

        _dispatcher.Dispatch(() =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Fullscreen state changed: {IsFullscreen}", isFullscreen);
            }

            if (!_keyboardBacklightMonitor.HasObservedLevel)
            {
                return;
            }

            byte currentLevel = _keyboardBacklightMonitor.CurrentLevel;
            _ledBehavior.OnFullscreenChanged(isFullscreen, currentLevel);
            UpdateLedReapplyLoopState();
        });
    }
}
