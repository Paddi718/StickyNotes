using System.IO;
using System.Text.Json.Serialization;
using StickyNotes.Models;
using StickyNotes.Services.Interfaces;
using StickyNotes.Utilities;

namespace StickyNotes.Services;

/// <summary>
/// JSON-backed note repository. Reads/writes %AppData%\StickyNotes\notes.json
/// using atomic writes to prevent corruption on crash.
/// </summary>
public sealed class NoteService : INoteService
{
    private readonly ISettingsService _settings;
    private readonly object _sync = new();
    private List<Note> _notes = new();

    // Cascade new notes across the screen so they don't all stack at (100,100)
    private int _createCounter;

    public NoteService(ISettingsService settings)
    {
        _settings = settings;
    }

    public IReadOnlyList<Note> LoadAll()
    {
        lock (_sync)
        {
            try
            {
                var data = JsonHelper.ReadFile<NotesData>(PathHelper.NotesFilePath);
                if (data?.Notes != null)
                {
                    _notes = data.Notes;
                    Logger.Info($"Loaded {_notes.Count} note(s) from {PathHelper.NotesFilePath}");
                }
                else
                {
                    _notes = new List<Note>();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"LoadAll failed, attempting to preserve corrupt file. {ex.Message}", ex);
                BackupCorruptFile();
                _notes = new List<Note>();
            }
            return _notes.ToList();
        }
    }

    public IReadOnlyList<Note> GetAll()
    {
        lock (_sync)
        {
            return _notes.ToList();
        }
    }

    public Note Create()
    {
        var s = _settings.Current;
        var offset = _createCounter * 30;
        _createCounter++;

        var note = new Note
        {
            Title = "",
            Content = "",
            Color = s.DefaultColor,
            X = 100 + offset,
            Y = 100 + offset,
            Width = s.DefaultWidth,
            Height = s.DefaultHeight,
            FontSize = s.DefaultFontSize,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        lock (_sync)
        {
            _notes.Add(note);
            SaveInternal();
        }

        Logger.Info($"Created note {note.Id} at ({note.X},{note.Y})");
        return note;
    }

    public void Update(Note note)
    {
        if (note == null) return;

        lock (_sync)
        {
            var index = _notes.FindIndex(n => n.Id == note.Id);
            if (index < 0)
            {
                Logger.Warn($"Update: note {note.Id} not found in cache, adding.");
                _notes.Add(note);
            }
            else
            {
                _notes[index] = note;
            }
            note.UpdatedAt = DateTime.Now;
            SaveInternal();
        }
    }

    public void Delete(Guid id)
    {
        lock (_sync)
        {
            var removed = _notes.RemoveAll(n => n.Id == id);
            if (removed > 0)
            {
                SaveInternal();
                Logger.Info($"Deleted note {id}");
            }
        }
    }

    public void Save()
    {
        lock (_sync)
        {
            SaveInternal();
        }
    }

    private void SaveInternal()
    {
        var data = new NotesData
        {
            Version = 1,
            Notes = _notes
        };

        if (JsonHelper.WriteFileAtomic(PathHelper.NotesFilePath, data))
            return;

        // Atomic write failed — already logged inside JsonHelper.
        // In-memory cache remains authoritative; next Save attempt may succeed.
    }

    private static void BackupCorruptFile()
    {
        try
        {
            if (!File.Exists(PathHelper.NotesFilePath)) return;
            var backup = PathHelper.NotesFilePath + ".corrupt";
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(PathHelper.NotesFilePath, backup);
            Logger.Warn($"Corrupt notes file backed up to {backup}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to back up corrupt notes file: {ex.Message}", ex);
        }
    }

    /// <summary>Serialization wrapper carrying a version for future migrations.</summary>
    private sealed class NotesData
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("notes")]
        public List<Note> Notes { get; set; } = new();
    }
}
