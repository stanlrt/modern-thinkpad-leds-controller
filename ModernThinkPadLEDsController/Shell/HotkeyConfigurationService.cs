using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ModernThinkPadLEDsController.Presentation.Services;
using ModernThinkPadLEDsController.Settings;

namespace ModernThinkPadLEDsController.Shell;

public readonly record struct HotkeyCaptureResult(
    bool CaptureCompleted,
    string DisplayText,
    string? WarningMessage);

/// <summary>
/// Applies hotkey configuration changes without routing the view through App.
/// </summary>
public sealed class HotkeyConfigurationService
{
    private readonly HotkeyService _hotkey;
    private readonly SettingsPersistenceService _settingsPersistence;
    private readonly MainPresentationService _presentation;
    private readonly ILogger<HotkeyConfigurationService> _logger;

    public HotkeyConfigurationService(
        HotkeyService hotkey,
        SettingsPersistenceService settingsPersistence,
        MainPresentationService presentation,
        ILogger<HotkeyConfigurationService> logger)
    {
        _hotkey = hotkey;
        _settingsPersistence = settingsPersistence;
        _presentation = presentation;
        _logger = logger;
    }

    public HotkeyCaptureResult CaptureHotkey(HotkeyBinding hotkey)
    {
        if (IsModifierKey(hotkey.Key))
        {
            return new HotkeyCaptureResult(false, string.Empty, null);
        }

        string displayText = _presentation.FormatHotkeyDisplay(hotkey);
        bool success = UpdateHotkey(hotkey, displayText);

        if (!success)
        {
            return new HotkeyCaptureResult(
                true,
                GetHotkeyDisplayText(),
                $"The hotkey '{displayText}' is already in use by Windows or another application. Please choose a different combination.");
        }

        string? warningMessage = hotkey.Modifiers == HotkeyModifiers.None
            ? $"'{displayText}' has no modifier keys. This may interfere with normal keyboard usage."
            : null;

        return new HotkeyCaptureResult(true, displayText, warningMessage);
    }

    public bool UpdateHotkey(HotkeyBinding hotkey, string displayText)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Updating hotkey to {Display} (modifiers={Modifiers:X}, vk={VirtualKey:X})",
                displayText,
                (int)hotkey.Modifiers,
                hotkey.VirtualKey);
        }

        bool success = _hotkey.UpdateHotkey(hotkey);
        if (!success)
        {
            _logger.LogWarning("Failed to register hotkey {Display} - already in use", displayText);
            return false;
        }

        _settingsPersistence.UpdateHotkey(hotkey);
        return true;
    }

    public string GetHotkeyDisplayText()
    {
        return _presentation.FormatHotkeyDisplay(_settingsPersistence.Hotkey);
    }

    private static bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl
            || key == Key.RightCtrl
            || key == Key.LeftAlt
            || key == Key.RightAlt
            || key == Key.LeftShift
            || key == Key.RightShift
            || key == Key.LWin
            || key == Key.RWin;
    }
}
