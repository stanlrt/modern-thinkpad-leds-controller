using System.Globalization;
using System.Windows.Data;

namespace ModernThinkPadLEDsController.Presentation.Converters;

/// <summary>
/// <para>Allows a RadioButton to be bound to an enum property.</para>
/// <para>
/// Usage:
///   IsChecked="{Binding Mode,
///       Converter={StaticResource EnumToBool},
///       ConverterParameter={x:Static lighting:LedMode.On}}"
/// </para>
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.Equals(parameter) == true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is not null ? parameter : System.Windows.Data.Binding.DoNothing;
}
