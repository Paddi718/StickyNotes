using StickyNotes.Models;

namespace StickyNotes.Services.Interfaces;

/// <summary>
/// Manages the lifecycle of all open note windows: creation, opening from
/// persisted data, hiding, deletion, and remounting after desktop changes.
/// </summary>
public interface INoteWindowManager
{
    bool HasOpenWindows { get; }

    /// <summary>Load all notes from storage and open a window for each.</summary>
    void OpenAll();

    /// <summary>Open a single note window. Called after creating or loading a note.</summary>
    void OpenNote(Note note);

    /// <summary>Create a new note via the note service and open it.</summary>
    void NewNote();

    /// <summary>Close (and optionally delete) a note window.</summary>
    /// <param name="id">Note id.</param>
    /// <param name="delete">True to delete the note from storage; false to just hide the window.</param>
    void CloseNote(Guid id, bool delete);

    /// <summary>Show all hidden note windows.</summary>
    void ShowAll();

    /// <summary>Hide all note windows (notes remain in storage).</summary>
    void HideAll();

    /// <summary>Remount every open window onto the (possibly new) WorkerW.</summary>
    void RemountAll();
}
