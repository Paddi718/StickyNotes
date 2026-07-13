using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Binding = System.Windows.Data.Binding;
using Brushes = System.Windows.Media.Brushes;
using StickyNotes.Models;
using StickyNotes.Themes;

namespace StickyNotes.Converters;

/// <summary>
/// Converts a NoteColor to its corresponding background or foreground brush.
/// Parameter "Foreground" selects the foreground brush; otherwise background.
/// </summary>
public sealed class ColorToBrushConverter : IValueConverter
{
    public static readonly ColorToBrushConverter Background = new();
    public static readonly ColorToBrushConverter Foreground = new(useForeground: true);

    private readonly bool _useForeground;

    public ColorToBrushConverter() : this(useForeground: false) { }

    public ColorToBrushConverter(bool useForeground)
    {
        _useForeground = useForeground;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not NoteColor color) return Brushes.Transparent;

        var wantForeground = _useForeground
            || (parameter is string s && s.Equals("Foreground", StringComparison.OrdinalIgnoreCase));

        return wantForeground
            ? NotePalette.GetForegroundBrush(color)
            : NotePalette.GetBackgroundBrush(color);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
