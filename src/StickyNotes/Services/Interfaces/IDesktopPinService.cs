using System.Windows;

namespace StickyNotes.Services.Interfaces;

/// <summary>
/// Pins note windows to the Windows desktop so they are immune to Win+D
/// ("Show Desktop"). Combines two mechanisms:
///   - Primary: hook WM_WINDOWPOSCHANGING to reject minimization/hiding at
///     the Win32 level — no flash, the window simply stays visible.
///   - Fallback (Method B): if the window is ever minimized despite the hook,
///     restore it immediately via StateChanged.
///
/// NOTE: The previous Method A (SetParent to WorkerW) was removed because
/// WPF's render target (HwndTarget/DirectComposition) cannot span process
/// boundaries — a WPF window reparented to Progman (owned by explorer.exe)
/// never paints its content.
/// </summary>
public interface IDesktopPinService : IDisposable
{
    /// <summary>True if at least one window is currently pinned.</summary>
    bool IsMounted { get; }

    /// <summary>Raised when a system event requires re-pinning all windows.</summary>
    event EventHandler? RemountRequired;

    /// <summary>Install the WM_WINDOWPOSCHANGING hook on the given window
    /// (primary mechanism), and subscribe to StateChanged for fallback.</summary>
    /// <returns>True if the hook was installed successfully.</returns>
    bool PinToDesktop(Window window);

    /// <summary>Remove the hook and restore normal window behavior.</summary>
    void UnpinFromDesktop(Window window);

    /// <summary>Re-pin every currently-tracked window.</summary>
    void RemountAll();

    /// <summary>Create a hidden message-only window and hook system broadcasts
    /// (TaskbarCreated, WM_DISPLAYCHANGE, WM_SETTINGCHANGE).</summary>
    void StartSystemEventListening();

    /// <summary>True if the last pin operation fell back to Method B only
    /// (hook installation failed).</summary>
    bool IsLastPinFallback { get; }
}
