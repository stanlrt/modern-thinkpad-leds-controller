using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Monitoring;

namespace ModernThinkPadLEDsController.Services;

public interface ISettingsRuntimeService
{
    void UpdateBlinkInterval(int intervalMs);
    void UpdateDiskPollInterval(int intervalMs);
    bool TryGetCurrentKeyboardBrightness(out byte level);
    void SetKeyboardBrightnessLevel(int level);
    void SetFullscreenPollingEnabled(bool enabled);
    bool IsStartupEnabled();
    StartupTaskOperationResult SetStartupEnabled(bool enabled);
}

public sealed class SettingsRuntimeService : ISettingsRuntimeService
{
    private readonly LedBehaviorService _ledBehavior;
    private readonly DiskActivityMonitor _diskMonitor;
    private readonly LedController _ledController;
    private readonly PowerEventListener _powerListener;

    public SettingsRuntimeService(
        LedBehaviorService ledBehavior,
        DiskActivityMonitor diskMonitor,
        LedController ledController,
        PowerEventListener powerListener)
    {
        _ledBehavior = ledBehavior;
        _diskMonitor = diskMonitor;
        _ledController = ledController;
        _powerListener = powerListener;
    }

    public void UpdateBlinkInterval(int intervalMs)
    {
        _ledBehavior.UpdateBlinkInterval(intervalMs);
    }

    public void UpdateDiskPollInterval(int intervalMs)
    {
        _diskMonitor.UpdateInterval(intervalMs);
    }

    public bool TryGetCurrentKeyboardBrightness(out byte level)
    {
        return _ledController.GetKeyboardBacklightRaw(out level);
    }

    public void SetKeyboardBrightnessLevel(int level)
    {
        _ledController.SetKeyboardBacklightRaw((byte)level);
    }

    public void SetFullscreenPollingEnabled(bool enabled)
    {
        if (enabled)
            _powerListener.StartFullscreenPolling();
        else
            _powerListener.StopFullscreenPolling();
    }

    public bool IsStartupEnabled()
    {
        return StartupTaskManager.IsRegistered();
    }

    public StartupTaskOperationResult SetStartupEnabled(bool enabled)
    {
        if (!enabled)
            return StartupTaskManager.Unregister();

        string executablePath = Environment.ProcessPath ?? string.Empty;
        return StartupTaskManager.Register(executablePath);
    }
}
