using StickyNotes.Models;

namespace StickyNotes.Services.Interfaces;

/// <summary>
/// Manages the lifecycle and persistence of sticky notes.
/// All methods are thread-safe.
/// </summary>
public interface INoteService
{
    /// <summary>Load all notes from disk into memory and return them.</summary>
    IReadOnlyList<Note> LoadAll();

    /// <summary>Return currently cached notes without re-reading disk.</summary>
    IReadOnlyList<Note> GetAll();

    /// <summary>Create a new note with defaults from settings, persist immediately.</summary>
    Note Create();

    /// <summary>Update an existing note (matched by Id) and persist.</summary>
    void Update(Note note);

    /// <summary>Delete a note by Id and persist.</summary>
    void Delete(Guid id);

    /// <summary>Force-write the in-memory cache to disk.</summary>
    void Save();
}
