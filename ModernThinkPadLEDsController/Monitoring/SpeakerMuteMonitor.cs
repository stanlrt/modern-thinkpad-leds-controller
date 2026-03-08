using NAudio.CoreAudioApi;

namespace ModernThinkPadLEDsController.Monitoring;

// SpeakerMuteMonitor monitors the system speaker (playback) mute state.
// This is similar to MicrophoneMuteMonitor but for the default audio output.
//
// When the user presses the physical mute button (e.g., F1 on ThinkPads),
// this monitor detects the change and fires MuteStateChanged.
//
// Convention (matches ThinkPad keyboard behaviour):
//   Speakers MUTED  → Mute LED ON  (red warning indicator)
//   Speakers ACTIVE → Mute LED OFF
/// <summary>
/// Observes the default speaker mute state.
/// </summary>
public sealed class SpeakerMuteMonitor : IDisposable
{
    // MuteStateChanged fires with: true = speakers are muted (LED should be ON)
    public event Action<bool>? MuteStateChanged;

    private readonly MMDeviceEnumerator _enumerator = new();
    private CancellationTokenSource? _cts;
    private bool _lastState;

    private const int PollIntervalMs = 500;

    public void Start()
    {
        // Sync the initial state immediately before starting the poll loop.
        _lastState = QueryMuted();

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => PollLoop(_cts.Token));
    }

    public void Stop() => _cts?.Cancel();

    // Returns the current mute state without going through the event system.
    public bool QueryMuted() => IsSpeakerMuted();

    private async Task PollLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            bool muted = IsSpeakerMuted();
            if (muted != _lastState)
            {
                _lastState = muted;
                MuteStateChanged?.Invoke(muted);
            }

            try { await Task.Delay(PollIntervalMs, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private bool IsSpeakerMuted()
    {
        try
        {
            // GetDefaultAudioEndpoint(DataFlow.Render) = the default speakers/headphones.
            using MMDevice? device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return device?.AudioEndpointVolume?.Mute ?? false;
        }
        catch { return false; } // no audio output — treat as unmuted
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _enumerator.Dispose();
    }
}
