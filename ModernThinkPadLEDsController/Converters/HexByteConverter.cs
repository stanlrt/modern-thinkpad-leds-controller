using System.Globalization;
using System.Windows.Data;

namespace ModernThinkPadLEDsController.Converters;

// Converts between byte? and hex string (e.g., "0F" <-> 15).
// Allows users to input LED register IDs in hex format.
[ValueConversion(typeof(byte?), typeof(string))]
public sealed class HexByteConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is byte b)
            return b.ToString("X2"); // e.g., 15 → "0F"

        return string.Empty; // null → empty string
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string str || string.IsNullOrWhiteSpace(str))
            return null; // empty → null

        // Try parse hex string
        if (byte.TryParse(str, NumberStyles.HexNumber, culture, out byte result))
            return result;

        return null; // invalid input → null (keeps existing value)
    }
}
