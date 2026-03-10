using ModernThinkPadLEDsController.Hardware;

namespace ModernThinkPadLEDsController.Lighting;

/// <summary>
/// Tracks which LEDs are in Blink mode and actively toggles them
/// </summary>
public sealed class LedBlinkController : ILedBlinkController
{
    private readonly LedController _leds;
    private readonly Dictionary<Led, byte?> _blinkingLeds = new();
    private readonly object _lock = new();

    private CancellationTokenSource? _cts;
    private bool _currentState;
    private bool _isPaused;
    private int _blinkIntervalMs;

    public LedBlinkController(LedController leds, int blinkIntervalMs = 500)
    {
        _leds = leds;
        _blinkIntervalMs = blinkIntervalMs;
    }

    /// <summary>
    /// Update the blink interval dynamically
    /// </summary>
    public void SetBlinkInterval(int intervalMs)
    {
        _blinkIntervalMs = intervalMs;
    }

    public void Start()
    {
        Stop(); // Stop any existing blink loop
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => BlinkLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Add an LED to the blinking set
    /// </summary>
    public void AddBlinkingLed(Led led, byte? customId)
    {
        lock (_lock)
        {
            _blinkingLeds[led] = customId;

            // Start the blink loop if this is the first LED
            if (_blinkingLeds.Count == 1 && _cts == null)
            {
                Start();
            }
        }
    }

    /// <summary>
    /// Remove an LED from the blinking set
    /// </summary>
    public void RemoveBlinkingLed(Led led)
    {
        lock (_lock)
        {
            _blinkingLeds.Remove(led);

            // Stop the blink loop if no LEDs remain
            if (_blinkingLeds.Count == 0)
            {
                Stop();
            }
        }
    }

    /// <summary>
    /// Clear all blinking LEDs
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            _blinkingLeds.Clear();
            Stop();
        }
    }

    /// <summary>
    /// Pause blinking without removing LEDs from the list
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            _isPaused = true;
        }
    }

    /// <summary>
    /// Resume blinking
    /// </summary>
    public void Resume()
    {
        lock (_lock)
        {
            _isPaused = false;
        }
    }

    private async Task BlinkLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _currentState = !_currentState;
            LedState state = _currentState ? LedState.On : LedState.Off;

            // Toggle all blinking LEDs (unless paused)
            lock (_lock)
            {
                if (!_isPaused)
                {
                    foreach ((Led led, byte? customId) in _blinkingLeds)
                    {
                        _leds.SetLed(led, state, customId);
                    }
                }
            }

            try
            {
                await Task.Delay(_blinkIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
