namespace ModernThinkPadLEDsController.Lighting;

/// <summary>
/// The behaviour assigned to a single LED.
/// Exactly one mode is active at a time
/// </summary>
public enum LedMode
{
    /// <summary>Don't override — let the OS / BIOS manage this LED</summary>
    Default = 0,

    /// <summary>Force the LED on permanently.</summary>
    On = 1,

    /// <summary>Force the LED off permanently.</summary>
    Off = 2,

    /// <summary>Force the LED to blink continuously.</summary>
    Blink = 3,

    /// <summary>Cycle through the configured states each time Win+Shift+K is pressed.</summary>
    HotkeyControlled = 4,

    /// <summary>Turn the LED on whenever disk write activity is detected.</summary>
    DiskWrite = 5,

    /// <summary>Turn the LED on whenever disk read activity is detected.</summary>
    DiskRead = 6,
}
