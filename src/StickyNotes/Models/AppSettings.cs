namespace StickyNotes.Models;

/// <summary>
/// Application-wide settings persisted to settings.json.
/// Includes toggles for both desktop-pinning methods (A: WorkerW mount,
/// B: minimize-restore fallback).
/// </summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public bool AutoStartWithWindows { get; set; } = false;

    public bool ShowTrayIcon { get; set; } = true;

    public bool ConfirmOnDelete { get; set; } = true;

    public NoteColor DefaultColor { get; set; } = NoteColor.Yellow;

    public double DefaultWidth { get; set; } = 260;

    public double DefaultHeight { get; set; } = 260;

    public double DefaultFontSize { get; set; } = 14;

    /// <summary>
    /// Primary mechanism: WM_WINDOWPOSCHANGING hook that prevents the window
    /// from being minimized/hidden by Win+D, Win+M, or "Show Desktop". Blocks
    /// the state change at the Win32 level — no flash.
    /// (Previously Method A: SetParent to WorkerW — removed because WPF cannot
    /// render cross-process. Setting name kept for config compatibility.)
    /// </summary>
    public bool EnableMethodA { get; set; } = true;

    /// <summary>
    /// Method B: listen for StateChanged == Minimized and restore immediately.
    /// Fallback in case the primary hook misses a minimization path.
    /// </summary>
    public bool EnableMethodB { get; set; } = true;

    /// <summary>
    /// Where to mount the note window. WorkerW = wallpaper layer (above icons),
    /// Progman = icon layer (below icons). Default WorkerW.
    /// </summary>
    public MountTarget MountTarget { get; set; } = MountTarget.WorkerW;

    public int RemountRetryDelayMs { get; set; } = 500;

    public int RemountMaxAttempts { get; set; } = 3;
}

public enum MountTarget
{
    WorkerW = 0,
    Progman = 1
}
