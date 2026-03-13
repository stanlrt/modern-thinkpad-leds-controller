using NAudio.CoreAudioApi;

namespace ModernThinkPadLEDsController.Monitoring;

/// <summary>
/// Observes the default microphone mute state.
/// </summary>
public sealed class MicrophoneMuteMonitor : ILifecycleMonitor
{
    /// <summary>
    /// MuteStateChanged fires with: true = microphone is muted (LED should be ON)
    /// </summary>
    public event Action<bool>? MuteStateChanged;

    /// <summary>
    /// MMDeviceEnumerator is the NAudio class that lets us find audio devices.
    /// We create it once and reuse it — creating it is expensive.
    /// </summary>
    private readonly MMDeviceEnumerator _enumerator = new();
    private CancellationTokenSource? _cts;
    private bool _lastState;

    private const int POLL_INTERVAL_MS = 500;

    public void Start()
    {
        Stop();

        // Sync the initial state immediately before starting the poll loop.
        _lastState = QueryMuted();

        _cts = new CancellationTokenSource();
        Task.Run(() => PollLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Returns the current mute state without going through the event system.
    /// Called at startup so the LED reflects reality before the first poll fires.
    /// </summary>
    public bool QueryMuted() => IsMicrophoneMuted();

    private async Task PollLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            bool muted = IsMicrophoneMuted();
            if (muted != _lastState)
            {
                _lastState = muted;
                MuteStateChanged?.Invoke(muted);
            }

            try { await Task.Delay(POLL_INTERVAL_MS, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private bool IsMicrophoneMuted()
    {
        try
        {
            // GetDefaultAudioEndpoint(DataFlow.Capture) = the default microphone.
            // We use 'using' so the COM object is released immediately after reading.
            using MMDevice? device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            return device?.AudioEndpointVolume?.Mute ?? false;
        }
        catch { return false; } // no microphone installed — treat as unmuted
    }

    public void Dispose()
    {
        Stop();
        _enumerator.Dispose();
    }
}
