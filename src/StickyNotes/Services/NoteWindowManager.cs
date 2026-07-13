using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StickyNotes.Models;
using StickyNotes.Services.Interfaces;
using StickyNotes.Utilities;
using StickyNotes.ViewModels;
using StickyNotes.Views;

namespace StickyNotes.Services;

/// <summary>
/// Coordinates note windows with the note service and desktop-pin service.
/// Each note gets its own NoteWindow + NoteViewModel pair, tracked by id.
/// </summary>
public sealed class NoteWindowManager : INoteWindowManager
{
    private readonly IServiceProvider _services;
    private readonly INoteService _noteService;
    private readonly IDesktopPinService _pinService;
    private readonly ISettingsService _settings;
    private readonly Dictionary<Guid, NoteWindow> _windows = new();
    private readonly object _sync = new();

    public bool HasOpenWindows
    {
        get
        {
            lock (_sync) { return _windows.Count > 0; }
        }
    }

    public NoteWindowManager(
        IServiceProvider services,
        INoteService noteService,
        IDesktopPinService pinService,
        ISettingsService settings)
    {
        _services = services;
        _noteService = noteService;
        _pinService = pinService;
        _settings = settings;
    }

    public void OpenAll()
    {
        var notes = _noteService.LoadAll();
        if (notes.Count == 0)
        {
            Logger.Info("No notes found — creating a welcome note.");
            var welcome = _noteService.Create();
            welcome.Title = "欢迎使用便签";
            welcome.Content = "这是你的第一条便签！\r\n\r\n• 拖动顶部标题栏移动便签\r\n• 拖动边缘调整大小\r\n• 右上角按钮可新建/换色/删除/隐藏\r\n• 按 Win+D 不会最小化本便签\r\n• 关闭后重启会自动恢复\r\n\r\n享受高效桌面便签体验！";
            _noteService.Update(welcome);
            OpenNote(welcome);
            return;
        }

        foreach (var note in notes)
        {
            OpenNote(note);
        }

        Logger.Info($"Opened {notes.Count} note window(s).");
    }

    public void OpenNote(Note note)
    {
        if (note == null) return;

        lock (_sync)
        {
            if (_windows.TryGetValue(note.Id, out var existing))
            {
                // Already open — just bring forward
                existing.Show();
                existing.Activate();
                return;
            }
        }

        Logger.Info($"OpenNote: creating window for note {note.Id} at ({note.X},{note.Y}) size {note.Width}x{note.Height}.");

        // Create ViewModel via DI (transient), injecting the note
        var viewModel = ActivatorUtilities.CreateInstance<NoteViewModel>(_services, note);

        var window = new NoteWindow(viewModel)
        {
            Left = note.X,
            Top = note.Y,
            Width = note.Width,
            Height = note.Height
        };

        lock (_sync)
        {
            _windows[note.Id] = window;
        }

        // Pin BEFORE show so the WM_WINDOWPOSCHANGING hook is in place before
        // the window receives any system messages. PinToDesktop calls
        // EnsureHandle internally to obtain the HWND, then installs the hook.
        // No SetParent is involved (WPF can't render cross-process), so the
        // ordering is not as critical as before — but pinning first is still
        // cleaner and ensures the hook catches the initial show.
        SafeExec.Try(() =>
        {
            if (!_pinService.PinToDesktop(window))
            {
                Logger.Warn($"Window for note {note.Id} running in fallback mode (Method B only)..");
            }
        });

        window.Show();
    }

    public void NewNote()
    {
        var note = _noteService.Create();
        OpenNote(note);
    }

    public void CloseNote(Guid id, bool delete)
    {
        NoteWindow? window;
        lock (_sync)
        {
            if (!_windows.TryGetValue(id, out window))
                return;
        }

        // Detach from pin tracking before closing
        _pinService.UnpinFromDesktop(window);

        if (delete)
        {
            _noteService.Delete(id);
            lock (_sync) { _windows.Remove(id); }
            window.ForceClose();
            Logger.Info($"Closed and deleted note {id}.");
        }
        else
        {
            // Just hide — note remains in storage and can be re-shown from tray
            window.Hide();
            Logger.Info($"Hid note {id}.");
        }
    }

    public void ShowAll()
    {
        List<NoteWindow> snapshot;
        lock (_sync) { snapshot = _windows.Values.ToList(); }

        foreach (var window in snapshot)
        {
            // Pin BEFORE show for consistency with OpenNote. PinToDesktop is
            // idempotent (skips if the hook is already installed), so this is
            // a cheap no-op for windows that never lost their mount.
            SafeExec.Try(() => _pinService.PinToDesktop(window));
            window.Show();
        }
    }

    public void HideAll()
    {
        List<NoteWindow> snapshot;
        lock (_sync) { snapshot = _windows.Values.ToList(); }

        foreach (var window in snapshot)
        {
            window.Hide();
        }
    }

    public void RemountAll()
    {
        _pinService.RemountAll();
    }
}
