namespace ModernThinkPadLEDsController;

[Flags]
public enum HotkeyCycleOptions
{
    None = 0,
    On = 1 << 0,
    Off = 1 << 1,
    Blink = 1 << 2,
}
