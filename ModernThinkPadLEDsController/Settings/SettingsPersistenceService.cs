using Microsoft.Extensions.Logging;
using System.Windows.Input;
using ModernThinkPadLEDsController.Presentation.Services;
using ModernThinkPadLEDsController.Shell;

namespace ModernThinkPadLEDsController.Settings;

/// <summary>
/// Persists application settings to durable storage.
/// </summary>
public sealed class SettingsPersistenceService
{
    private readonly AppSettings _settings;
    private readonly MainPresentationService _presentation;
    private readonly ILogger<SettingsPersistenceService> _logger;

    public SettingsPersistenceService(
        AppSettings settings,
        MainPresentationService presentation,
        ILogger<SettingsPersistenceService> logger)
    {
        _settings = settings;
        _presentation = presentation;
        _logger = logger;
    }

    public bool PersistSettingsOnChange => _settings.PersistSettingsOnChange;

    public HotkeyModifiers HotkeyModifiers => _settings.HotkeyModifiers;

    public Key HotkeyKey => _settings.HotkeyKey;

    public void SaveCurrentSettings()
    {
        try
        {
            _logger.LogDebug("Saving settings");
            _presentation.SaveToSettings();
            _settings.Save();
            _logger.LogDebug("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    public void UpdateHotkey(HotkeyModifiers modifiers, Key key)
    {
        _settings.HotkeyModifiers = modifiers;
        _settings.HotkeyKey = key;

        if (!PersistSettingsOnChange)
            return;

        _settings.Save();
        _logger.LogDebug("Hotkey settings saved");
    }
}
