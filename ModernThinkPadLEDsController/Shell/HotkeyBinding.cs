using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace ModernThinkPadLEDsController.Shell;

/// <summary>
/// Represents a complete hotkey binding as modifier flags plus a virtual-key code.
/// </summary>
public sealed class HotkeyBinding
{
    private enum ModifierVirtualKey
    {
        Shift = 0x10,
        Control = 0x11,
        Alt = 0x12,
        LWin = 0x5B,
        RWin = 0x5C,
    }

    private static readonly (ModifierVirtualKey Vk, HotkeyModifiers Mod)[] _modifierMap =
    [
        (ModifierVirtualKey.Control, HotkeyModifiers.Control),
        (ModifierVirtualKey.Alt,     HotkeyModifiers.Alt),
        (ModifierVirtualKey.Shift,   HotkeyModifiers.Shift),
        (ModifierVirtualKey.LWin,    HotkeyModifiers.Win),
        (ModifierVirtualKey.RWin,    HotkeyModifiers.Win),
    ];

    [DllImport("user32.dll")]
    private static extern short GetKeyState(ModifierVirtualKey virtualKey);

    public HotkeyModifiers Modifiers { get; set; } = AppSettingsDefaults.HOTKEY_MODIFIERS;
    public int VirtualKey { get; set; } = AppSettingsDefaults.HOTKEY_VIRTUAL_KEY;

    public static HotkeyBinding FromCurrentKeyPress(Key key)
    {
        return new HotkeyBinding
        {
            Modifiers = GetActiveModifiers(),
            Key = key,
        };
    }


    [JsonIgnore]
    public Key Key
    {
        get => KeyInterop.KeyFromVirtualKey(VirtualKey);
        set => VirtualKey = KeyInterop.VirtualKeyFromKey(value);
    }

    private static HotkeyModifiers GetActiveModifiers()
    {
        HotkeyModifiers modifiers = HotkeyModifiers.None;
        foreach ((ModifierVirtualKey vk, HotkeyModifiers mod) in _modifierMap)
        {
            if (IsKeyDown(vk))
            {
                modifiers |= mod;
            }
        }
        return modifiers;
    }

    private static bool IsKeyDown(ModifierVirtualKey virtualKey)
    {

        const int KEY_DOWN_MASK = 0x8000;
        return (GetKeyState(virtualKey) & KEY_DOWN_MASK) != 0;
    }
}
