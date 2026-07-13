namespace StickyNotes.Native;

/// <summary>
/// Win32 constants used by the desktop-pinning mechanism.
/// </summary>
internal static class WindowConstants
{
    // Window styles
    public const long WS_CHILD = 0x40000000;
    public const long WS_POPUP = 0x80000000;
    public const long WS_VISIBLE = 0x10000000;
    public const long WS_CAPTION = 0x00C00000;
    public const long WS_THICKFRAME = 0x00040000;
    public const long WS_MINIMIZEBOX = 0x00020000;
    public const long WS_MAXIMIZEBOX = 0x00010000;
    public const long WS_MINIMIZE = 0x20000000;

    // Extended window styles
    public const long WS_EX_LAYERED = 0x00080000;
    public const long WS_EX_TOOLWINDOW = 0x00000080;  // Hides window from Alt+Tab

    // GetWindowLong indices
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    // GetWindow constants
    public const uint GW_HWNDFIRST = 0;
    public const uint GW_HWNDLAST = 1;
    public const uint GW_HWNDNEXT = 2;
    public const uint GW_HWNDPREV = 3;
    public const uint GW_OWNER = 4;
    public const uint GW_CHILD = 5;

    // SetLayeredWindowAttributes flags
    public const uint LWA_COLORKEY = 0x00000001;
    public const uint LWA_ALPHA = 0x00000002;

    // SendMessageTimeout flags
    public const uint SMTO_NORMAL = 0x0000;
    public const uint SMTO_BLOCK = 0x0001;
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    // ShowWindow commands
    public const int SW_HIDE = 0;
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOW = 5;
    public const int SW_SHOWNA = 8;

    // SetWindowPos flags
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_HIDEWINDOW = 0x0080;
    public const uint SWP_STATECHANGED = 0x8000;

    // RedrawWindow flags
    public const uint RDW_INVALIDATE = 0x0001;
    public const uint RDW_UPDATENOW = 0x0100;
    public const uint RDW_ALLCHILDREN = 0x0080;
    public const uint RDW_ERASE = 0x0004;

    // HWND insert-after constants
    public static readonly IntPtr HWND_TOP = IntPtr.Zero;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    // Window messages relevant to desktop/shell changes
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_SETTINGCHANGE = 0x001A;
    public const int WM_DWMCOMPOSITIONCHANGED = 0x031E;
    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int WM_ERASEBKGND = 0x0014;
    public const int WM_NCCALCSIZE = 0x0083;
    public const int WM_NCHITTEST = 0x0084;
    public const int WM_STYLECHANGED = 0x007D;

    // WM_NCHITTEST return values (non-client area identifiers)
    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;

    // SPI parameters for WM_SETTINGCHANGE wParam
    public const uint SPI_SETDESKWALLPAPER = 0x0014;
    public const uint SPI_SETWORKAREA = 0x002F;

    /// <summary>
    /// Undocumented message sent to Progman to force creation of a WorkerW
    /// window (used to render the wallpaper layer above desktop icons).
    /// </summary>
    public const uint WM_SPAWN_WORKERW = 0x052C;

    // Window class names
    public const string ProgmanClass = "Progman";
    public const string WorkerWClass = "WorkerW";
    public const string ShellDefViewClass = "SHELLDLL_DefView";

    // DWM window attributes (Win11 22000+)
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;        // Win11 22000+
    public const int DWMWA_BORDER_COLOR = 34;
    public const int DWMWA_CAPTION_COLOR = 35;
    public const int DWMWA_TEXT_COLOR = 36;

    // DWM window corner preference values
    public const int DWMWCP_DEFAULT = 0;
    public const int DWMWCP_DONOTROUND = 1;
    public const int DWMWCP_ROUND = 2;
    public const int DWMWCP_ROUNDSMALL = 3;

    // DWM system backdrop types (for DWMWA_SYSTEMBACKDROP_TYPE)
    public const int DWMSBT_AUTO = 0;             // Let DWM decide
    public const int DWMSBT_NONE = 1;             // No backdrop
    public const int DWMSBT_MAINWINDOW = 2;       // Mica
    public const int DWMSBT_TRANSIENTWINDOW = 3;  // Acrylic (transient windows)
    public const int DWMSBT_TABBEDWINDOW = 4;     // Tabbed Mica

    // ---------- Accent Policy (SetWindowCompositionAttribute) ----------
    // Undocumented but stable API for blur/acrylic behind windows.
    // Works with WPF AllowsTransparency=False when combined with WS_EX_LAYERED.

    public const int WCA_ACCENT_POLICY = 19;

    public const int ACCENT_DISABLED = 0;
    public const int ACCENT_ENABLE_GRADIENT = 1;
    public const int ACCENT_ENABLE_TRANSPARENTGRADIENT = 2;
    public const int ACCENT_ENABLE_BLURBEHIND = 3;            // Blur, no tint
    public const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;     // Acrylic blur + tint

    // ---------- WinEvent (foreground window tracking) ----------

    /// <summary>
    /// Fired when the foreground window changes (user clicks a window, Win+D
    /// brings the desktop forward, Alt+Tab, etc.). Used to dynamically toggle
    /// TOPMOST on note windows so they stay above the desktop but can be
    /// covered by normal application windows.
    /// </summary>
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

    /// <summary>
    /// Out-of-context delivery: the callback is posted to the installing
    /// thread's message queue (must be pumping messages). Safe for use from
    /// a managed UI thread — the callback runs on the thread that called
    /// SetWinEventHook, not the event source's thread.
    /// </summary>
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    /// <summary>Skip events generated by our own process (avoids self-loops).</summary>
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    /// <summary>idObject value indicating the event is for the whole window.</summary>
    public const int OBJID_WINDOW = 0;
}
