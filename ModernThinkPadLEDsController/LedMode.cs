namespace ModernThinkPadLEDsController;

/// <summary>
/// The behaviour assigned to a single LED.
/// Exactly one mode is active at a time
/// </summary>
public enum LedMode
{
    /// <summary>Don't override — let the OS / BIOS manage this LED</summary>
    Default,

    /// <summary>Force the LED on permanently.</summary>
    On,

    /// <summary>Force the LED off permanently.</summary>
    Off,

    /// <summary>Force the LED to blink continuously.</summary>
    Blink,

    /// <summary>Cycle through the configured states each time Win+Shift+K is pressed.</summary>
    HotkeyControlled,

    /// <summary>Turn the LED on whenever disk write activity is detected.</summary>
    DiskWrite,

    /// <summary>Turn the LED on whenever disk read activity is detected.</summary>
    DiskRead,
}
