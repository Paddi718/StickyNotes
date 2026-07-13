using System.Windows.Media;
using System.Windows.Threading;
using StickyNotes.Models;
using StickyNotes.Services.Interfaces;
using StickyNotes.Themes;
using StickyNotes.Utilities;

namespace StickyNotes.ViewModels;

/// <summary>
/// ViewModel for a single sticky note. Owns the Note model, exposes
/// bindable properties, and persists changes with a 1-second debounce
/// to avoid writing to disk on every keystroke.
/// </summary>
public sealed class NoteViewModel : ViewModelBase
{
    private readonly Note _note;
    private readonly INoteService _noteService;
    private readonly INoteWindowManager _windowManager;
    private readonly DispatcherTimer _saveTimer;

    public NoteViewModel(Note note, INoteService noteService, INoteWindowManager windowManager)
    {
        _note = note ?? throw new ArgumentNullException(nameof(note));
        _noteService = noteService;
        _windowManager = windowManager;

        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _saveTimer.Tick += OnSaveTimerTick;

        Title = note.Title;
        // Convert Markdown checkboxes → Unicode glyphs for display
        Content = note.Content
            .Replace("- [x] ", "☑ ")
            .Replace("- [ ] ", "☐ ");
        _color = note.Color;
        _fontSize = note.FontSize;
        _lineSpacing = note.LineSpacing;
        _opacity = note.Opacity;
        _isLocked = note.IsLocked;
        _fontColor = note.FontColor;
        _updatedAt = note.UpdatedAt;
    }

    // ---------- Bindable properties ----------

    private string _title = "";
    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                _note.Title = value;
                ScheduleSave();
            }
        }
    }

    private string _content = "";
    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value))
            {
                // Store in Markdown format for disk, display with glyphs in memory
                _note.Content = ToStorageFormat(value);
                ScheduleSave();
            }
        }
    }

    private NoteColor _color;
    public NoteColor Color
    {
        get => _color;
        set
        {
            if (SetProperty(ref _color, value))
            {
                _note.Color = value;
                OnPropertyChanged(nameof(BackgroundBrush));
                OnPropertyChanged(nameof(ForegroundBrush));
                ScheduleSave();
            }
        }
    }

    private double _fontSize;
    public double FontSize
    {
        get => _fontSize;
        set
        {
            // Clamp to a sensible range
            var clamped = Math.Clamp(value, 10, 36);
            if (SetProperty(ref _fontSize, clamped))
            {
                _note.FontSize = clamped;
                ScheduleSave();
            }
        }
    }

    private FontColor _fontColor;
    public FontColor FontColor
    {
        get => _fontColor;
        set
        {
            if (SetProperty(ref _fontColor, value))
            {
                _note.FontColor = value;
                OnPropertyChanged(nameof(ForegroundBrush));
                ScheduleSave();
            }
        }
    }

    private double _opacity;
    public double Opacity
    {
        get => _opacity;
        set
        {
            var clamped = Math.Clamp(value, 0.1, 1.0);
            if (SetProperty(ref _opacity, clamped))
            {
                _note.Opacity = clamped;
                OnPropertyChanged(nameof(BackgroundBrush));
                ScheduleSave();
            }
        }
    }

    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (SetProperty(ref _isLocked, value))
            {
                _note.IsLocked = value;
                ScheduleSave();
            }
        }
    }

    private DateTime _updatedAt;
    public DateTime UpdatedAt
    {
        get => _updatedAt;
        private set => SetProperty(ref _updatedAt, value);
    }

    // ---------- Derived brushes (bound from XAML) ----------

    public SolidColorBrush BackgroundBrush => NotePalette.GetBackgroundBrush(Color, Opacity);
    public SolidColorBrush ForegroundBrush => NotePalette.GetForegroundBrush(Color, FontColor);

    public Guid NoteId => _note.Id;

    // ---------- Commands ----------

    public RelayCommand NewNoteCommand => new(_ => _windowManager.NewNote());

    public RelayCommand CloseCommand => new(_ => _windowManager.CloseNote(_note.Id, delete: false));

    public RelayCommand DeleteCommand => new(_ => _windowManager.CloseNote(_note.Id, delete: true));

    public RelayCommand CycleColorCommand => new(_ => Color = NotePalette.Next(Color));

    public RelayCommand CycleFontColorCommand => new(_ => FontColor = NotePalette.NextFontColor(FontColor));

    public RelayCommand IncreaseFontCommand => new(_ => FontSize += 1);

    public RelayCommand DecreaseFontCommand => new(_ => FontSize -= 1);

    private double _lineSpacing;
    public double LineSpacing
    {
        get => _lineSpacing;
        set
        {
            var clamped = Math.Clamp(value, 1.0, 2.5);
            if (SetProperty(ref _lineSpacing, clamped))
            {
                _note.LineSpacing = clamped;
                ScheduleSave();
            }
        }
    }

    public RelayCommand IncreaseLineSpacingCommand => new(_ => LineSpacing += 0.25);
    public RelayCommand DecreaseLineSpacingCommand => new(_ => LineSpacing -= 0.25);

    public RelayCommand ToggleLockCommand => new(_ => IsLocked = !IsLocked);

    /// <summary>
    /// Toggles checkbox glyph on the current line (☐ ↔ ☑).
    /// Content is stored with Unicode glyphs; conversion to Markdown
    /// happens only on save.
    /// </summary>
    public void ToggleTodoAtCursor(int cursorPos)
    {
        var text = Content;
        if (string.IsNullOrEmpty(text)) return;

        var lineStart = text.LastIndexOf('\n', Math.Min(cursorPos, text.Length - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var lineEnd = text.IndexOf('\n', lineStart);
        if (lineEnd < 0) lineEnd = text.Length;

        var line = text[lineStart..lineEnd];
        var before = text[..lineStart];
        var after = text[lineEnd..];

        if (line.StartsWith("☑ "))
            // Uncheck: remove strikethrough + toggle glyph
            Content = before + "☐ " + StripStrikethrough(line[2..]) + after;
        else if (line.StartsWith("☐ "))
            // Check: add strikethrough
            Content = before + "☑ " + ApplyStrikethrough(line[2..]) + after;
        else
            // Plain text → todo (unchecked, no strikethrough)
            Content = before + "☐ " + line + after;
    }

    private const char StrikeChar = '̶'; // Combining Long Stroke Overlay

    private static string ApplyStrikethrough(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length * 2);
        foreach (var c in s)
        {
            sb.Append(c);
            if (c > ' ') sb.Append(StrikeChar); // Only visible chars
        }
        return sb.ToString();
    }

    private static string StripStrikethrough(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            if (c != StrikeChar) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>Converts display glyphs back to Markdown for disk storage.</summary>
    private string ToStorageFormat(string display) =>
        display.Replace("☑ ", "- [x] ").Replace("☐ ", "- [ ] ");

    // ---------- Position & size write-back (called from window code-behind) ----------

    public void UpdatePosition(double x, double y)
    {
        if (Math.Abs(_note.X - x) < 0.5 && Math.Abs(_note.Y - y) < 0.5) return;
        _note.X = x;
        _note.Y = y;
        ScheduleSave();
    }

    public void UpdateSize(double width, double height)
    {
        if (Math.Abs(_note.Width - width) < 0.5 && Math.Abs(_note.Height - height) < 0.5) return;
        _note.Width = width;
        _note.Height = height;
        ScheduleSave();
    }

    /// <summary>Flush any pending debounced save immediately (e.g., on app exit).</summary>
    public void FlushSave()
    {
        if (_saveTimer.IsEnabled)
        {
            _saveTimer.Stop();
            SaveNow();
        }
    }

    // ---------- Debounced save ----------

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        SaveNow();
    }

    private void SaveNow()
    {
        _note.UpdatedAt = DateTime.Now;
        UpdatedAt = _note.UpdatedAt;
        SafeExec.Try(() => _noteService.Update(_note));
    }
}
