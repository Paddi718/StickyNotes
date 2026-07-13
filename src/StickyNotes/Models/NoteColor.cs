namespace StickyNotes.Models;

/// <summary>
/// Built-in note color palettes. Values are serialized as integers in notes.json.
/// Add new entries at the END to keep existing stored values stable.
/// </summary>
public enum NoteColor
{
    Yellow = 0,
    Pink = 1,
    Green = 2,
    Blue = 3,
    Purple = 4,
    Orange = 5,
    White = 6,
    Dark = 7
}
