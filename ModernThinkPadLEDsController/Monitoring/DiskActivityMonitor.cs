using System.Diagnostics;

namespace ModernThinkPadLEDsController.Monitoring;

// DiskActivityState tells us what kind of I/O is happening right now.
public enum DiskActivityState { Idle, Read, Write, ReadWrite }

// DiskActivityMonitor replaces the BackgroundWorker + PerformanceCounter loop
// in the legacy Form1.workerHDD_DoWork. It runs on a background thread and
// fires StateChanged whenever the disk activity state changes.
//
// PerformanceCounter is a Windows system counter — the same data you see
// in Task Manager's disk graph. It reads the "_Total" disk bytes per second.
public sealed class DiskActivityMonitor : IDisposable
{
    // StateChanged fires on the background thread. The caller must dispatch
    // back to the UI thread if they want to update UI properties.
    public event Action<DiskActivityState>? StateChanged;

    // IsAvailable is false if the disk performance counters are disabled on
    // this machine (can happen on some enterprise/hardened systems).
    public bool IsAvailable { get; private set; }

    private PerformanceCounter? _readCounter;
    private PerformanceCounter? _writeCounter;
    private CancellationTokenSource? _cts;
    private int _intervalMs;

    public DiskActivityMonitor(int intervalMs = 300)
    {
        _intervalMs = intervalMs;
    }

    // Call TryInitialize() once at startup. Returns false if the Windows
    // disk performance counters are not available on this machine.
    public bool TryInitialize()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("LogicalDisk")) return false;
            if (!PerformanceCounterCategory.CounterExists("Disk Read Bytes/sec", "LogicalDisk")) return false;
            if (!PerformanceCounterCategory.CounterExists("Disk Write Bytes/sec", "LogicalDisk")) return false;

            _readCounter = new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", "_Total");
            _writeCounter = new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", "_Total");
            IsAvailable = true;
            return true;
        }
        catch { return false; }
    }

    public void Start()
    {
        if (!IsAvailable) return;
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

    // Call when the user changes the HDD poll interval slider.
    public void UpdateInterval(int ms) => Interlocked.Exchange(ref _intervalMs, ms);

    private async Task MonitorLoop(CancellationToken ct)
    {
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

            StateChanged?.Invoke(state);

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
