using ModernThinkPadLEDsController.Hardware;

namespace ModernThinkPadLEDsController.Monitoring;

// KeyboardBacklightMonitor does two things:
//
// 1. It continuously polls EC register 0x0D to watch for user-initiated
//    backlight changes (e.g. pressing Fn+Space). When the level changes,
//    it fires LevelChanged so the UI stays in sync.
//
// 2. It maintains a rolling history of the last 5 readings. When the system
//    wakes from sleep or the lid opens, PowerEventListener calls
//    RestoreMostCommonLevel() to put the keyboard backlight back to where
//    the user had it. The "most common" approach avoids restoring a wrong
//    level if there was one bad reading right before sleep.
//
// This replaces the legacy 'levels' List<LightLevel> + LINQ query pattern
// in Form1.cs, but the core idea is identical.
public sealed class KeyboardBacklightMonitor : IDisposable
{
    public event Action<KeyboardBacklight>? LevelChanged;

    private readonly LedController _leds;
    private readonly Queue<KeyboardBacklight> _history = new();
    private const int HistorySize    = 5;
    private const int PollIntervalMs = 1000;

    private CancellationTokenSource? _cts;
    private KeyboardBacklight _lastSeen = KeyboardBacklight.Off;

    public KeyboardBacklightMonitor(LedController leds) => _leds = leds;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => PollLoop(_cts.Token));
    }

    public void Stop() => _cts?.Cancel();

    // Called by PowerEventListener on system resume and lid-open.
    public void RestoreMostCommonLevel()
    {
        if (_history.Count == 0) return;

        KeyboardBacklight mostCommon = _history
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .First().Key;

        _leds.SetKeyboardBacklight(mostCommon);
    }

    // Returns the most recently observed level (used by App.xaml.cs to
    // save the level into AppSettings on shutdown).
    public KeyboardBacklight CurrentLevel => _lastSeen;

    private async Task PollLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_leds.GetKeyboardBacklight(out KeyboardBacklight level))
            {
                if (_history.Count >= HistorySize)
                    _history.Dequeue();
                _history.Enqueue(level);

                if (level != _lastSeen)
                {
                    _lastSeen = level;
                    LevelChanged?.Invoke(level);
                }
            }

            try { await Task.Delay(PollIntervalMs, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    public void Dispose() => _cts?.Cancel();
}
