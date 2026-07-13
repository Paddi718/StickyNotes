namespace StickyNotes.Models;

/// <summary>
/// A single sticky note. Persisted to notes.json.
/// Position uses virtual-screen coordinates (SystemParameters.VirtualScreenLeft/Top),
/// so values can be negative on multi-monitor setups.
/// </summary>
public sealed class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "";

    public string Content { get; set; } = "";

    public NoteColor Color { get; set; } = NoteColor.Yellow;

    public double X { get; set; } = 100;

    public double Y { get; set; } = 100;

    public double Width { get; set; } = 260;

    public double Height { get; set; } = 260;

    public double FontSize { get; set; } = 14;

    /// <summary>Foreground (text) color preset. <c>Auto</c> derives a readable
    /// color from <see cref="Color"/>; other values are explicit overrides for
    /// when low <see cref="Opacity"/> makes the default text hard to read.</summary>
    public FontColor FontColor { get; set; } = FontColor.Auto;

    /// <summary>Background opacity (0.1–1.0). Controls alpha of the note's
    /// background brush over the DWM acrylic backdrop.</summary>
    public double Opacity { get; set; } = 0.75;

    /// <summary>When true, the note's controls are hidden, content is
    /// read-only, and the window cannot be dragged.</summary>
    public bool IsLocked { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Creates a deep copy for safe in-memory manipulation.
    /// </summary>
    public Note Clone() => (Note)MemberwiseClone();
}
