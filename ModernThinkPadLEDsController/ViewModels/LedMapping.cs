using CommunityToolkit.Mvvm.ComponentModel;

namespace ModernThinkPadLEDsController.ViewModels;

// LedMapping holds every user-configurable flag for a single LED.
//
// [ObservableProperty] is a CommunityToolkit.Mvvm source-generator attribute.
// It looks at the private field '_hddRead' and automatically generates:
//   - a public property 'HddRead' with get/set
//   - a PropertyChanged notification on every set
//   - an OnHddReadChanged() partial method you can override if needed
//
// Without the source generator you'd have to write all of that boilerplate
// by hand for each property. The generator does it at compile time.
public sealed partial class LedMapping : ObservableObject
{
    [ObservableProperty] private bool _hddRead;
    [ObservableProperty] private bool _hddWrite;
    [ObservableProperty] private bool _capsLock;
    [ObservableProperty] private bool _numLock;

    // When Invert is true, On↔Off are swapped whenever this LED is driven
    // by the monitoring system. Blink is never inverted.
    [ObservableProperty] private bool _invert;
}
