namespace ModernThinkPadLEDsController.Shell;

[Flags]
public enum HotkeyModifiers
{
    None = 0x0000,
    Alt = 0x0001,
    Control = 1 << 1,
    Shift = 1 << 2,
    Win = 1 << 3,
}
