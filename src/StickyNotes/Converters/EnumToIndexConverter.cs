using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;

namespace StickyNotes.Converters;

/// <summary>
/// Converts between an enum value and its integer index (for binding enums
/// to ComboBox.SelectedIndex). The enum order must match the ComboBox items.
/// </summary>
public sealed class EnumToIndexConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return 0;
        var enumType = value.GetType();
        if (!enumType.IsEnum) return 0;
        var values = Enum.GetValues(enumType);
        return Array.IndexOf(values, value);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int index || !targetType.IsEnum) return Binding.DoNothing;
        var values = Enum.GetValues(targetType);
        if (index < 0 || index >= values.Length) return Binding.DoNothing;
        return values.GetValue(index);
    }
}
