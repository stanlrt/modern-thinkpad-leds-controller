using Microsoft.Extensions.Logging;

namespace ModernThinkPadLEDsController.Services;

public sealed class SettingsCoordinator
{
    private readonly AppSettings _settings;
    private readonly MainUiController _ui;
    private readonly ILogger<SettingsCoordinator> _logger;

    public SettingsCoordinator(
        AppSettings settings,
        MainUiController ui,
        ILogger<SettingsCoordinator> logger)
    {
        _settings = settings;
        _ui = ui;
        _logger = logger;
    }

    public bool PersistSettingsOnChange => _settings.PersistSettingsOnChange;

    public int HotkeyModifiers => _settings.HotkeyModifiers;

    public int HotkeyVirtualKey => _settings.HotkeyVirtualKey;

    public void SaveCurrentSettings()
    {
        try
        {
            _logger.LogDebug("Saving settings");
            _ui.SaveToSettings();
            _settings.Save();
            _logger.LogDebug("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    public void UpdateHotkey(int modifiers, int virtualKey)
    {
        _settings.HotkeyModifiers = modifiers;
        _settings.HotkeyVirtualKey = virtualKey;

        if (!PersistSettingsOnChange)
            return;

        _settings.Save();
        _logger.LogDebug("Hotkey settings saved");
    }
}
