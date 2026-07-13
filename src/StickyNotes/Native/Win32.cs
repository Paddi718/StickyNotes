using System.Runtime.InteropServices;

namespace StickyNotes.Native;

/// <summary>
/// P/Invoke declarations for the desktop-pinning mechanism.
/// Grouped by purpose: window lookup, enumeration, reparenting, style, messaging.
/// </summary>
internal static class Win32
{
    // ---------- Window lookup ----------

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindowExW(
        IntPtr hWndParent,
        IntPtr hWndChildAfter,
        string? lpszClass,
        string? lpszWindow);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetClassNameW(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    // ---------- Enumeration ----------

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    // ---------- Reparenting ----------

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetParent(IntPtr hWnd);

    // ---------- Window style ----------

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetWindowLongW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SetWindowLongW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    // 64-bit safe variants (used for pointer-width indices; GWL_STYLE works with the W versions above)
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    // ---------- Messaging ----------

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SendMessageTimeoutW(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint RegisterWindowMessageW(string lpString);

    // ---------- ShowWindow / SetWindowPos ----------

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RedrawWindow(
        IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateLayeredWindow(
        IntPtr hWnd, IntPtr hdcDst, ref Point pptDst, ref Size psize,
        IntPtr hdcSrc, ref Point pptSrc, uint crKey, ref BLENDFUNCTION pblend,
        uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetLayeredWindowAttributes(
        IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);

    /// <summary>
    /// Retrieves the screen coordinates of the entire window (including
    /// non-client area). Used by WM_NCHITTEST to compute edge distances
    /// for manual resize border hit-testing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // ---------- WinEvent (foreground window tracking) ----------

    public delegate void WinEventProc(
        IntPtr hWinEventHook, uint eventCode,
        IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventProc lpfnWinEventProc,
        uint idProcess, uint idThread,
        uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    // ---------- DWM ----------

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int value, int cbAttribute);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    /// <summary>
    /// Margins for DwmExtendFrameIntoClientArea. Setting all to -1 extends
    /// the DWM glass/frame across the entire client area, which is required
    /// for DWM system backdrops (acrylic/mica) to be visible through WPF
    /// windows that use AllowsTransparency=False.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    // ---------- Window Composition (Accent Policy) ----------
    // Undocumented but widely-used API for enabling blur/acrylic behind a
    // window. Works with WPF windows that have AllowsTransparency=False by
    // combining WS_EX_LAYERED + SetLayeredWindowAttributes + accent policy.

    [StructLayout(LayoutKind.Sequential)]
    public struct ACCENTPOLICY
    {
        public int nAccentState;
        public int nFlags;
        public uint nColor;      // 0xAABBGGRR format
        public int nAnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINCOMPATTRDATA
    {
        public int nAttribute;
        public IntPtr pvData;
        public int cbData;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WINCOMPATTRDATA data);

    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Size { public int Width; public int Height; }

    [StructLayout(LayoutKind.Sequential)]
    public struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    /// <summary>
    /// WINDOWPOS struct, passed via lParam in WM_WINDOWPOSCHANGING.
    /// Modifying its flags in-place (via Marshal.StructureToPtr) lets us
    /// selectively reject window state changes (e.g. prevent minimization
    /// when Win+D tries to minimize all top-level windows).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    // ---------- Convenience helpers ----------

    /// <summary>
    /// Returns the class name of the given window, or empty string on failure.
    /// </summary>
    public static string GetClassName(IntPtr hWnd)
    {
        var buffer = new char[256];
        var length = GetClassNameW(hWnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    /// <summary>
    /// Unified GetWindowLong that works on both 32/64-bit for GWL_STYLE.
    /// </summary>
    public static long GetWindowStyle(IntPtr hWnd) =>
        GetWindowLongW(hWnd, WindowConstants.GWL_STYLE).ToInt64();

    /// <summary>
    /// Unified SetWindowLong for GWL_STYLE.
    /// </summary>
    public static void SetWindowStyle(IntPtr hWnd, long style) =>
        SetWindowLongW(hWnd, WindowConstants.GWL_STYLE, new IntPtr(style));

    /// <summary>
    /// Unified GetWindowLong for GWL_EXSTYLE (extended window style).
    /// </summary>
    public static long GetWindowExStyle(IntPtr hWnd) =>
        GetWindowLongW(hWnd, WindowConstants.GWL_EXSTYLE).ToInt64();

    /// <summary>
    /// Unified SetWindowLong for GWL_EXSTYLE.
    /// </summary>
    public static void SetWindowExStyle(IntPtr hWnd, long style) =>
        SetWindowLongW(hWnd, WindowConstants.GWL_EXSTYLE, new IntPtr(style));
}
