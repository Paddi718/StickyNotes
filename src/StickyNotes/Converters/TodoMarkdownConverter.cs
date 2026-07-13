using System;
using System.Globalization;
using System.Windows.Data;

namespace StickyNotes.Converters;

/// <summary>
/// Converts Markdown checkbox syntax to Unicode checkbox glyphs for display,
/// and back for storage.
///   Display: ☐ (U+2610) / ☑ (U+2611)
///   Storage: - [ ] / - [x]
/// </summary>
public class TodoMarkdownConverter : IValueConverter
{
    private const string UncheckedMd = "- [ ] ";
    private const string CheckedMd   = "- [x] ";
    private const string UncheckedCh = "☐ ";  // ☐
    private const string CheckedCh   = "☑ ";  // ☑

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? "";
        return text.Replace(CheckedMd, CheckedCh).Replace(UncheckedMd, UncheckedCh);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? "";
        return text.Replace(CheckedCh, CheckedMd).Replace(UncheckedCh, UncheckedMd);
    }
}
