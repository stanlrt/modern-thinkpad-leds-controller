using ModernThinkPadLEDsController.Hardware;
using ModernThinkPadLEDsController.Lighting;
using ModernThinkPadLEDsController.Monitoring;
using ModernThinkPadLEDsController.Shell;

namespace ModernThinkPadLEDsController.Settings;

/// <summary>
/// Applies settings changes that have immediate runtime effects.
/// </summary>
public interface ISettingsRuntimeService
{
    void UpdateBlinkInterval(int intervalMs);
    void UpdateDiskPollInterval(int intervalMs);
    bool TryGetCurrentKeyboardBrightness(out byte level);
    void SetKeyboardBrightnessLevel(int level);
    void SetFullscreenPollingEnabled(bool enabled);
    HardwareAccessPreferenceChangeResult SetHardwareAccessEnabled(bool enabled);
    string GetHardwareAccessStatus();
    bool IsStartupEnabled();
    StartupTaskOperationResult SetStartupEnabled(bool enabled);
}

/// <summary>
/// Bridges settings changes into live runtime behavior.
/// </summary>
public sealed class SettingsRuntimeService : ISettingsRuntimeService
{
    private readonly AppSettings _settings;
    private readonly HardwareAccessController _hardwareAccess;
    private readonly LedBehaviorService _ledBehavior;
    private readonly DiskActivityMonitor _diskMonitor;
    private readonly KeyboardBacklightMonitor _keyboardBacklightMonitor;
    private readonly LedController _ledController;
    private readonly FullscreenMonitor _fullscreenMonitor;

    public SettingsRuntimeService(
        AppSettings settings,
        HardwareAccessController hardwareAccess,
        LedBehaviorService ledBehavior,
        DiskActivityMonitor diskMonitor,
        KeyboardBacklightMonitor keyboardBacklightMonitor,
        LedController ledController,
        FullscreenMonitor fullscreenMonitor)
    {
        _settings = settings;
        _hardwareAccess = hardwareAccess;
        _ledBehavior = ledBehavior;
        _diskMonitor = diskMonitor;
        _keyboardBacklightMonitor = keyboardBacklightMonitor;
        _ledController = ledController;
        _fullscreenMonitor = fullscreenMonitor;
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
        if (!_hardwareAccess.IsEnabled)
            return;

        if (enabled)
            _fullscreenMonitor.Start();
        else
            _fullscreenMonitor.Stop();
    }

    public HardwareAccessPreferenceChangeResult SetHardwareAccessEnabled(bool enabled)
    {
        _settings.EnableHardwareAccess = enabled;

        if (enabled)
        {
            if (_hardwareAccess.IsEnabled)
                return new HardwareAccessPreferenceChangeResult(true, null);

            return new HardwareAccessPreferenceChangeResult(
                false,
                "Hardware access is saved for the next launch. Restart the app to load PawnIO and touch the EC again.");
        }

        bool disabledNow = _hardwareAccess.DisableForSession();
        _diskMonitor.Stop();
        _keyboardBacklightMonitor.Stop();
        _fullscreenMonitor.Stop();
        _ledBehavior.DisableHardwareActivity();

        return new HardwareAccessPreferenceChangeResult(
            disabledNow,
            disabledNow
                ? "Hardware access is disabled immediately for this session and will stay off on the next launch."
                : null);
    }

    public string GetHardwareAccessStatus()
    {
        return _hardwareAccess.GetStatusDescription();
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
