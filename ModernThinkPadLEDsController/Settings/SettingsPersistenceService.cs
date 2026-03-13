using Microsoft.Extensions.Logging;
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

    public HotkeyBinding Hotkey => _settings.Hotkey;

    public void SaveCurrentSettings()
    {
        try
        {
            _logger.LogDebug("Saving settings");
            _presentation.SaveToSettings();

            if (_settings.Save())
            {
                _logger.LogDebug("Settings saved successfully");
            }
            else
            {
                _logger.LogWarning("Settings save failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    public void UpdateHotkey(HotkeyBinding hotkey)
    {
        _settings.Hotkey = hotkey;

        if (!PersistSettingsOnChange)
        {
            return;
        }

        if (_settings.Save())
        {
            _logger.LogDebug("Hotkey settings saved");
        }
        else
        {
            _logger.LogWarning("Hotkey settings save failed");
        }
    }
}
