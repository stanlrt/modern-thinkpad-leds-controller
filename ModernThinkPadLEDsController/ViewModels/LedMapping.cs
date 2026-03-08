using CommunityToolkit.Mvvm.ComponentModel;

namespace ModernThinkPadLEDsController.ViewModels;

public sealed partial class LedMapping : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    [ObservableProperty] private LedMode _mode = LedMode.Default;
    [ObservableProperty] private byte? _customRegisterId;
}
