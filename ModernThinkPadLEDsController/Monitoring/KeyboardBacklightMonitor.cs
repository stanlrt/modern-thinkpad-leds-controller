using ModernThinkPadLEDsController.Hardware;

namespace ModernThinkPadLEDsController.Monitoring;

/// <summary>
/// Observes and restores the keyboard backlight level.
/// </summary>
public sealed class KeyboardBacklightMonitor : ILifecycleMonitor
{
    public event Action<byte>? LevelChanged;

    private readonly LedController _leds;
    private readonly Queue<byte> _history = new();
    private const int HISTORY_SIZE = 5;
    private const int POLL_INTERVAL_MS = 1000;

    private CancellationTokenSource? _cts;

    public KeyboardBacklightMonitor(LedController leds) => _leds = leds;

    public void Start()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        // Read initial level synchronously BEFORE starting the async polling loop
        // This ensures CurrentLevel is valid immediately when fullscreen polling starts
        if (_leds.GetKeyboardBacklightRaw(out byte initialLevel))
        {
            CurrentLevel = initialLevel;
            HasObservedLevel = true;
            _history.Enqueue(initialLevel);
        }

        _ = Task.Run(() => PollLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Called by PowerEventListener on system resume and lid-open.
    /// </summary>
    public void RestoreMostCommonLevel()
    {
        if (_history.Count == 0)
        {
            return;
        }

        byte mostCommon = _history
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .First().Key;

        _leds.SetKeyboardBacklightRaw(mostCommon);
    }

    /// <summary>
    /// Returns the most recently observed level (used by App.xaml.cs to
    /// save the level into AppSettings on shutdown).
    /// </summary>
    public byte CurrentLevel { get; private set; } = 0;

    /// <summary>
    /// True after at least one successful hardware read.
    /// </summary>
    public bool HasObservedLevel { get; private set; }

    private async Task PollLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_leds.GetKeyboardBacklightRaw(out byte level))
            {
                HasObservedLevel = true;

                if (_history.Count >= HISTORY_SIZE)
                {
                    _history.Dequeue();
                }

                _history.Enqueue(level);

                if (level != CurrentLevel)
                {
                    CurrentLevel = level;
                    LevelChanged?.Invoke(level);
                }
            }

            try { await Task.Delay(POLL_INTERVAL_MS, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
