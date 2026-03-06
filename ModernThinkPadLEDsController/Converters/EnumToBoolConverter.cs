using System.Globalization;
using System.Windows.Data;

namespace ModernThinkPadLEDsController.Converters;

/// <summary>
/// Allows a RadioButton to be bound to an enum property.
///
/// Usage:
///   IsChecked="{Binding Mode,
///       Converter={StaticResource EnumToBool},
///       ConverterParameter={x:Static root:LedMode.On}}"
///
/// Convert:     returns true when the current value equals the parameter.
/// ConvertBack: returns the parameter value when the radio button is checked;
///              returns Binding.DoNothing when it is unchecked (so the other
///              radio buttons do not accidentally reset the binding).
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.Equals(parameter) == true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is not null ? parameter : Binding.DoNothing;
}
