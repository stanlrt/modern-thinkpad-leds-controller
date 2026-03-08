namespace ModernThinkPadLEDsController.Shell;

[System.Flags]
public enum HotkeyModifiers
{
    None = 0x0000,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}
