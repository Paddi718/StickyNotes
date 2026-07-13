namespace StickyNotes.Models;

/// <summary>
/// Font color presets for note text. <c>Auto</c> derives a readable text
/// color from the note's background <see cref="NoteColor"/>; the other
/// values are explicit overrides the user can cycle through when the
/// default is hard to read (e.g. low opacity makes light text wash out).
/// Values are serialized as integers in notes.json — add new entries at
/// the END to keep existing stored values stable.
/// </summary>
public enum FontColor
{
    Auto = 0,
    Black = 1,
    White = 2,
    Red = 3,
    Orange = 4,
    Yellow = 5,
    Green = 6,
    Blue = 7,
    Purple = 8,
    Gray = 9
}
