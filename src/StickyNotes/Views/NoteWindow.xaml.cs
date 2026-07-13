using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ContextMenuEventArgs = System.Windows.Controls.ContextMenuEventArgs;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using StickyNotes.Native;
using StickyNotes.Themes;
using StickyNotes.Utilities;
using StickyNotes.ViewModels;

namespace StickyNotes.Views;

/// <summary>
/// Code-behind for the note window. Stays thin per MVVM convention:
/// only handles drag-move, position/size write-back to the ViewModel,
/// Win32 acrylic/blur setup, and cancels direct close (the WindowManager
/// decides hide vs delete).
/// </summary>
public partial class NoteWindow : Window
{
    private readonly NoteViewModel _viewModel;
    private bool _positionChanging;
    private IntPtr _hwnd;

    public NoteWindow(NoteViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        LocationChanged += OnLocationChanged;
        SizeChanged += OnSizeChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// HWND has just been created but not yet shown — the right moment to
    /// apply Win32 attributes: WS_EX_TOOLWINDOW (Alt+Tab hiding),
    /// accent-policy acrylic blur, and rounded corners.
    ///
    /// Uses SetWindowCompositionAttribute with ACCENT_ENABLE_ACRYLICBLURBEHIND
    /// and a fixed BLUR_TINT_ALPHA (decoupled from note opacity). The XAML
    /// BackgroundBrush still binds to the opacity slider for content tinting,
    /// so the user gets independent control of:
    ///   - Blur visibility (always on, fixed alpha)
    ///   - Content opacity (slider)
    ///
    /// Win+D immunity is preserved by the WM_WINDOWPOSCHANGING hook in
    /// DesktopPinService, which blocks minimization at the Win32 level.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        if (_hwnd == IntPtr.Zero) return;

        // Add WS_EX_TOOLWINDOW (hide from Alt+Tab).
        SafeExec.Try(() =>
        {
            var ex = Win32.GetWindowExStyle(_hwnd);
            var target = ex | WindowConstants.WS_EX_TOOLWINDOW;
            if (ex != target)
                Win32.SetWindowExStyle(_hwnd, target);
        });

        // Apply acrylic blur with fixed tint alpha (decoupled from note opacity).
        UpdateAcrylicTint();

        // Hook WM_ERASEBKGND (prevent default background fill) and
        // WM_NCHITTEST (manual resize border hit-testing).
        SafeExec.Try(() =>
        {
            var source = HwndSource.FromHwnd(_hwnd);
            source?.AddHook(WindowProcHook);
        });

        // Win11 native rounded corners (DWMWCP_ROUND)
        SafeExec.Try(() =>
        {
            int pref = WindowConstants.DWMWCP_ROUND;
            Win32.DwmSetWindowAttribute(_hwnd,
                WindowConstants.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref pref, sizeof(int));
        });

        // One-time verification log
        SafeExec.Try(() =>
        {
            var ex = Win32.GetWindowExStyle(_hwnd);
            Logger.Info($"NoteWindow init: EX=0x{ex:X}, LAYERED={((ex & WindowConstants.WS_EX_LAYERED) != 0)}, TOOLWINDOW={((ex & WindowConstants.WS_EX_TOOLWINDOW) != 0)}");
        });
    }

    /// <summary>
    /// Drives all visual layers from the opacity slider:
    ///   slider ≤ 0.11 → ACCENT_DISABLED, no border, no rounded corners, fully transparent
    ///   slider > 0.11 → ACCENT_ENABLE_ACRYLICBLURBEHIND with progressive alpha,
    ///                     glass border, rounded corners
    /// </summary>
    private void UpdateAcrylicTint()
    {
        if (_hwnd == IntPtr.Zero) return;

        var opacity = _viewModel.Opacity;
        var isTransparent = opacity <= 0.11;

        Dispatcher.Invoke(() =>
        {
            if (isTransparent)
            {
                // Kill all visuals that could create layered-window edge artifacts
                GlassBorder.CornerRadius = new CornerRadius(0);
                GlassBorder.ClipToBounds = false;
                GlassBorder.BorderThickness = new Thickness(0);
                GlassBorder.BorderBrush = null;
                GlassBorder.Background = new SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(1, 255, 255, 255));
            }
            else
            {
                // Restore frosted-glass card look
                GlassBorder.CornerRadius = new CornerRadius(12);
                GlassBorder.ClipToBounds = true;
                GlassBorder.BorderThickness = new Thickness(1);
                GlassBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("GlassBorderBrush");
                GlassBorder.Background = new SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(1, 255, 255, 255));
            }
        });

        SafeExec.Try(() =>
        {
            if (isTransparent)
            {
                // Fully transparent: no blur, no DWM corners
                var off = new Win32.ACCENTPOLICY
                {
                    nAccentState = WindowConstants.ACCENT_DISABLED,
                    nFlags = 0, nColor = 0, nAnimationId = 0
                };
                var d = new Win32.WINCOMPATTRDATA
                {
                    nAttribute = WindowConstants.WCA_ACCENT_POLICY,
                    pvData = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.ACCENTPOLICY>()),
                    cbData = Marshal.SizeOf<Win32.ACCENTPOLICY>()
                };
                try { Marshal.StructureToPtr(off, d.pvData, false); Win32.SetWindowCompositionAttribute(_hwnd, ref d); }
                finally { Marshal.FreeHGlobal(d.pvData); }

                int noRound = WindowConstants.DWMWCP_DONOTROUND;
                Win32.DwmSetWindowAttribute(_hwnd, WindowConstants.DWMWA_WINDOW_CORNER_PREFERENCE, ref noRound, sizeof(int));
                return;
            }

            // Frosted glass: enable acrylic + round corners
            int round = WindowConstants.DWMWCP_ROUND;
            Win32.DwmSetWindowAttribute(_hwnd, WindowConstants.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

            var baseBrush = NotePalette.GetBackgroundBrush(_viewModel.Color);
            var c = baseBrush.Color;
            var alpha = (byte)(0x20 + (opacity - 0.15) / 0.85 * 0x60);
            var accentColor = ((uint)alpha << 24) | ((uint)c.B << 16) | ((uint)c.G << 8) | (uint)c.R;

            var accent = new Win32.ACCENTPOLICY
            {
                nAccentState = WindowConstants.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                nFlags = 0,
                nColor = accentColor,
                nAnimationId = 0
            };
            var data = new Win32.WINCOMPATTRDATA
            {
                nAttribute = WindowConstants.WCA_ACCENT_POLICY,
                pvData = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.ACCENTPOLICY>()),
                cbData = Marshal.SizeOf<Win32.ACCENTPOLICY>()
            };
            try { Marshal.StructureToPtr(accent, data.pvData, false); Win32.SetWindowCompositionAttribute(_hwnd, ref data); }
            finally { Marshal.FreeHGlobal(data.pvData); }
        }, nameof(UpdateAcrylicTint));
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NoteViewModel.Opacity) or nameof(NoteViewModel.Color))
        {
            UpdateAcrylicTint();
        }
    }

    /// <summary>
    /// Window procedure hook that handles:
    ///   - WM_ERASEBKGND: prevents the default background fill (essential for
    ///     acrylic transparency — the window must not paint over the blur).
    ///   - WM_NCHITTEST: manual resize border hit-testing (replaces
    ///     WindowChrome's ResizeBorderThickness). Returns HT* values when
    ///     the cursor is near a window edge, enabling native Win32 resize.
    /// </summary>
    private IntPtr WindowProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WindowConstants.WM_ERASEBKGND:
                // Prevent the default background fill — essential for acrylic
                // transparency. The window must not paint over the blur.
                handled = true;
                return (IntPtr)1;

            case WindowConstants.WM_NCHITTEST:
                return HandleNcHitTest(hwnd, lParam, ref handled);
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Manual resize border hit-test. Without WindowChrome, WPF's default
    /// resize border is only ~2px. This extends it to 8 logical pixels
    /// (DPI-aware) around the window edges, matching the old WindowChrome
    /// ResizeBorderThickness="8".
    /// </summary>
    private IntPtr HandleNcHitTest(IntPtr hwnd, IntPtr lParam, ref bool handled)
    {
        // Extract cursor position from lParam (physical screen coordinates).
        // lParam: low word = X, high word = Y. Use short cast for signed
        // multi-monitor coordinates (can be negative on left/above origin).
        var lo = lParam.ToInt64() & 0xFFFF;
        var hi = (lParam.ToInt64() >> 16) & 0xFFFF;
        int x = unchecked((short)lo);
        int y = unchecked((short)hi);

        if (!Win32.GetWindowRect(hwnd, out var rect))
            return IntPtr.Zero;

        // Resize border thickness in physical pixels (8 logical px × DPI).
        var dpi = VisualTreeHelper.GetDpi(this);
        int bw = (int)Math.Ceiling(8 * dpi.DpiScaleX);
        int bh = (int)Math.Ceiling(8 * dpi.DpiScaleY);

        bool onLeft = x - rect.Left < bw;
        bool onRight = rect.Right - x < bw;
        bool onTop = y - rect.Top < bh;
        bool onBottom = rect.Bottom - y < bh;

        // Corners take priority over edges
        if (onTop && onLeft)    { handled = true; return (IntPtr)WindowConstants.HTTOPLEFT; }
        if (onTop && onRight)   { handled = true; return (IntPtr)WindowConstants.HTTOPRIGHT; }
        if (onBottom && onLeft) { handled = true; return (IntPtr)WindowConstants.HTBOTTOMLEFT; }
        if (onBottom && onRight){ handled = true; return (IntPtr)WindowConstants.HTBOTTOMRIGHT; }
        if (onLeft)   { handled = true; return (IntPtr)WindowConstants.HTLEFT; }
        if (onRight)  { handled = true; return (IntPtr)WindowConstants.HTRIGHT; }
        if (onTop)    { handled = true; return (IntPtr)WindowConstants.HTTOP; }
        if (onBottom) { handled = true; return (IntPtr)WindowConstants.HTBOTTOM; }

        // Not on a resize border — let WPF handle it (returns HTCLIENT).
        return IntPtr.Zero;
    }

    /// <summary>
    /// Title-bar drag using Preview (tunnel) event so we can intercept clicks
    /// BEFORE the title TextBox swallows them.
    ///
    /// Behavior:
    ///   - Click on a button        → button works normally (no drag)
    ///   - Single-click on title    → drag the window (TextBox stays unfocused)
    ///   - Double-click on title    → enter edit mode (focus + select all)
    ///   - Click while editing      → position cursor inside TextBox
    ///   - Click on empty title bar → drag
    ///   - Click while locked       → no drag (let clicks pass through)
    /// </summary>
    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // When locked, don't allow dragging — let clicks pass through to
        // the content (read-only) or context menu.
        if (_viewModel.IsLocked) return;

        // Let buttons handle their own clicks. The OriginalSource for a Button
        // with text content is a TextBlock inside a ContentPresenter, NOT the
        // Button itself — so we must walk the visual tree to detect it.
        if (e.OriginalSource is DependencyObject btnSource && FindAncestor<Button>(btnSource) != null)
            return;

        // Check if the click landed on the title TextBox
        if (e.OriginalSource is DependencyObject d && FindAncestor<TextBox>(d) is { } tb)
        {
            // If already editing, let the click position the cursor
            if (tb.IsKeyboardFocusWithin)
                return;

            // Double-click enters edit mode
            if (e.ClickCount >= 2)
            {
                tb.Focus();
                tb.SelectAll();
                e.Handled = true;
                return;
            }

            // Single click on non-editing TextBox → drag.
            // Mark handled so the TextBox doesn't steal focus.
            e.Handled = true;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove throws if the left button is already up — ignore
        }
        catch
        {
            // Swallow any other drag errors — never crash the note
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (_positionChanging) return;
        _positionChanging = true;
        try
        {
            _viewModel.UpdatePosition(Left, Top);
        }
        finally
        {
            _positionChanging = false;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.UpdateSize(Width, Height);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Flush any pending debounced save before the window goes away
        _viewModel.FlushSave();

        // Cancel direct close — WindowManager controls hide/delete.
        // The window is only truly closed via CloseNote(delete:true),
        // which removes it from tracking and then calls Close() with a flag.
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// Internal flag set by NoteWindowManager to allow a real close
    /// (after the note has been deleted from storage).
    /// </summary>
    internal bool _forceClose;

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T target) return target;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void Window_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            TopmostifyOwnedPopup();
        };
        timer.Start();
    }

    private void TopmostifyOwnedPopup()
    {
        if (_hwnd == IntPtr.Zero) return;
        var processId = (uint)Environment.ProcessId;
        const long WS_EX_TOPMOST = 0x8;

        Win32.EnumWindows((hwnd, lParam) =>
        {
            Win32.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != processId) return true;
            if (!Win32.IsWindowVisible(hwnd)) return true;
            if (hwnd == _hwnd) return true;

            var owner = Win32.GetWindow(hwnd, WindowConstants.GW_OWNER);
            if (owner != _hwnd) return true;

            var ex = Win32.GetWindowExStyle(hwnd);
            if ((ex & WS_EX_TOPMOST) == 0)
            {
                Win32.SetWindowExStyle(hwnd, ex | WS_EX_TOPMOST);
                Win32.SetWindowPos(hwnd, WindowConstants.HWND_TOPMOST, 0, 0, 0, 0,
                    WindowConstants.SWP_NOMOVE | WindowConstants.SWP_NOSIZE | WindowConstants.SWP_NOACTIVATE);
                Logger.Info($"ContextMenu popup 0x{hwnd:X}: set WS_EX_TOPMOST (owner=0x{_hwnd:X}).");
            }
            return true;
        }, IntPtr.Zero);
    }
}

