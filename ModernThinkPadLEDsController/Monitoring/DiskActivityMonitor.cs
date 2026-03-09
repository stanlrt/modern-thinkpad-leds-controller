using System.Diagnostics;

namespace ModernThinkPadLEDsController.Monitoring;

/// <summary>
/// DiskActivityState tells us what kind of I/O is happening right now.
/// </summary>
public enum DiskActivityState { Idle = 0, Read = 1, Write = 2, ReadWrite = 3 }

// DiskActivityMonitor replaces the BackgroundWorker + PerformanceCounter loop
// from the legacy WinForms app. It runs on a background thread and
// fires StateChanged whenever the disk activity state changes.
//
// PerformanceCounter is a Windows system counter — the same data you see
// in Task Manager's disk graph. It reads the "_Total" disk bytes per second.
/// <summary>
/// Observes aggregate disk read and write activity.
/// </summary>
public sealed class DiskActivityMonitor : ILifecycleMonitor
{
    public const int MIN_INTERVAL_MS = 100;

    /// <summary>
    /// StateChanged fires on the background thread. The caller must dispatch
    /// back to the UI thread if they want to update UI properties.
    /// </summary>
    public event Action<DiskActivityState>? StateChanged;

    /// <summary>
    /// IsAvailable is false if the disk performance counters are disabled on
    /// this machine (can happen on some enterprise/hardened systems).
    /// </summary>
    public bool IsAvailable { get; private set; }

    private PerformanceCounter? _readCounter;
    private PerformanceCounter? _writeCounter;
    private CancellationTokenSource? _cts;
    private int _intervalMs;

    public DiskActivityMonitor(int intervalMs = 300)
    {
        _intervalMs = Math.Max(MIN_INTERVAL_MS, intervalMs);
    }

    /// <summary>
    /// Call TryInitialize() once at startup. Returns false if the Windows
    /// disk performance counters are not available on this machine.
    /// </summary>
    public bool TryInitialize()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("LogicalDisk"))
            {
                return false;
            }

            if (!PerformanceCounterCategory.CounterExists("Disk Read Bytes/sec", "LogicalDisk"))
            {
                return false;
            }

            if (!PerformanceCounterCategory.CounterExists("Disk Write Bytes/sec", "LogicalDisk"))
            {
                return false;
            }

            _readCounter = new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", "_Total");
            _writeCounter = new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", "_Total");
            IsAvailable = true;
            return true;
        }
        catch { return false; }
    }

    public void Start()
    {
        if (!IsAvailable)
        {
            return;
        }
        // Stop any existing monitoring first to prevent multiple loops
        Stop();
        _cts = new CancellationTokenSource();
        // Task.Run starts the loop on a .NET thread-pool thread (background thread).
        _ = Task.Run(() => MonitorLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Call when the user changes the disk poll interval slider.
    /// </summary>
    public void UpdateInterval(int ms) => Interlocked.Exchange(ref _intervalMs, Math.Max(MIN_INTERVAL_MS, ms));

    private async Task MonitorLoop(CancellationToken ct)
    {
        DiskActivityState? lastState = null;

        while (!ct.IsCancellationRequested)
        {
            float r = _readCounter!.NextValue();
            float w = _writeCounter!.NextValue();

            // Pattern matching on a tuple — maps (hasRead, hasWrite) to a state value.
            DiskActivityState state = (r > 0, w > 0) switch
            {
                (true, true) => DiskActivityState.ReadWrite,
                (true, false) => DiskActivityState.Read,
                (false, true) => DiskActivityState.Write,
                _ => DiskActivityState.Idle,
            };

            if (lastState != state)
            {
                lastState = state;
                StateChanged?.Invoke(state);
            }

            try { await Task.Delay(_intervalMs, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    public void Dispose()
    {
        Stop();
        _readCounter?.Dispose();
        _writeCounter?.Dispose();
    }
}
