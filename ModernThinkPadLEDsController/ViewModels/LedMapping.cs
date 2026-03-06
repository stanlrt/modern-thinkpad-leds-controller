using CommunityToolkit.Mvvm.ComponentModel;

namespace ModernThinkPadLEDsController.ViewModels;

// LedMapping holds the single user-configurable mode for one LED.
// The mode is mutually exclusive (enum), replacing the old independent boolean flags.
// [ObservableProperty] generates the public Mode property + PropertyChanged notification.
public sealed partial class LedMapping : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    [ObservableProperty] private LedMode _mode = LedMode.Default;
}
