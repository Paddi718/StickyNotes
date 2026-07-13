using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using StickyNotes.Models;

namespace StickyNotes.Themes;

/// <summary>
/// Maps a NoteColor enum value to its frosted-background brush and
/// foreground (text) brush. Kept in code (not just XAML resources) so
/// ViewModels can bind directly without a converter lookup.
/// </summary>
public static class NotePalette
{
    public static SolidColorBrush GetBackgroundBrush(NoteColor color) => color switch
    {
        NoteColor.Yellow => CreateFrozen("#FFFFF3B0"),
        NoteColor.Pink   => CreateFrozen("#FFF8BBD0"),
        NoteColor.Green  => CreateFrozen("#FFC8E6C9"),
        NoteColor.Blue   => CreateFrozen("#FFB3E5FC"),
        NoteColor.Purple => CreateFrozen("#FFE1BEE7"),
        NoteColor.Orange => CreateFrozen("#FFFFD8A8"),
        NoteColor.White  => CreateFrozen("#FFF5F5F5"),
        NoteColor.Dark   => CreateFrozen("#FF2D2D30"),
        _                => CreateFrozen("#FFFFF3B0")
    };

    /// <summary>
    /// Returns a background brush with the specified opacity applied as the
    /// alpha channel. Used when DWM acrylic is active: the brush tints the
    /// acrylic backdrop, and lower opacity = more acrylic visible.
    /// The returned brush is NOT frozen (alpha changes per-note).
    /// </summary>
    public static SolidColorBrush GetBackgroundBrush(NoteColor color, double opacity)
    {
        var baseBrush = GetBackgroundBrush(color);
        var c = baseBrush.Color;
        var alpha = (byte)Math.Clamp((int)(255 * opacity), 1, 255);
        return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
    }

    public static SolidColorBrush GetForegroundBrush(NoteColor color) => color switch
    {
        NoteColor.Yellow => CreateFrozen("#FF3E2723"),
        NoteColor.Pink   => CreateFrozen("#FF3E2723"),
        NoteColor.Green  => CreateFrozen("#FF1B5E20"),
        NoteColor.Blue   => CreateFrozen("#FF0D47A1"),
        NoteColor.Purple => CreateFrozen("#FF311B92"),
        NoteColor.Orange => CreateFrozen("#FF3E2723"),
        NoteColor.White  => CreateFrozen("#FF212121"),
        NoteColor.Dark   => CreateFrozen("#FFFFFFFF"),
        _                => CreateFrozen("#FF3E2723")
    };

    /// <summary>
    /// Returns the foreground brush honoring an explicit <paramref name="fontColor"/>
    /// override. When <see cref="FontColor.Auto"/>, falls back to the background-
    /// derived color from <see cref="GetForegroundBrush(NoteColor)"/>.
    /// </summary>
    public static SolidColorBrush GetForegroundBrush(NoteColor color, FontColor fontColor)
    {
        if (fontColor == FontColor.Auto)
            return GetForegroundBrush(color);

        return fontColor switch
        {
            FontColor.Black  => CreateFrozen("#000000"),
            FontColor.White  => CreateFrozen("#FFFFFF"),
            FontColor.Red    => CreateFrozen("#D32F2F"),
            FontColor.Orange => CreateFrozen("#E65100"),
            FontColor.Yellow => CreateFrozen("#F57F17"),
            FontColor.Green  => CreateFrozen("#1B5E20"),
            FontColor.Blue   => CreateFrozen("#0D47A1"),
            FontColor.Purple => CreateFrozen("#4A148C"),
            FontColor.Gray   => CreateFrozen("#424242"),
            _                => GetForegroundBrush(color)
        };
    }

    public static NoteColor Next(NoteColor color)
    {
        var values = (NoteColor[])Enum.GetValues(typeof(NoteColor));
        var index = Array.IndexOf(values, color);
        return values[(index + 1) % values.Length];
    }

    /// <summary>Cycles to the next <see cref="FontColor"/> preset (Auto → Black → … → Gray → Auto).</summary>
    public static FontColor NextFontColor(FontColor color)
    {
        var values = (FontColor[])Enum.GetValues(typeof(FontColor));
        var index = Array.IndexOf(values, color);
        return values[(index + 1) % values.Length];
    }

    /// <summary>
    /// Creates a frozen SolidColorBrush from a hex string.
    /// Frozen brushes are thread-safe and performant for repeated binding.
    /// </summary>
    private static SolidColorBrush CreateFrozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
