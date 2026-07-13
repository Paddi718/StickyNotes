using System.Runtime.InteropServices;
using System.Windows;
using Application = System.Windows.Application;
using System.Windows.Interop;
using System.Windows.Threading;
using StickyNotes.Models;
using StickyNotes.Native;
using StickyNotes.Services.Interfaces;
using StickyNotes.Utilities;

namespace StickyNotes.Services;

/// <summary>
/// Desktop-pinning service implementing the dual-mechanism strategy:
///   Primary: hook WM_WINDOWPOSCHANGING to reject minimization/hiding at the
///     Win32 level. When Win+D / Win+M / "Show Desktop" tries to minimize all
///     top-level windows, the hook strips SWP_STATECHANGED and SWP_HIDEWINDOW
///     from the WINDOWPOS struct, so the window never changes state — no flash.
///   Fallback (Method B): subscribe to StateChanged; if the window is ever
///     minimized despite the hook (e.g. programmatic ShowWindow), restore it.
///
/// Also listens for system broadcasts (TaskbarCreated, WM_DISPLAYCHANGE) to
/// detect scenarios where hooks may have been lost (Explorer restart) and
/// trigger automatic re-pinning of all tracked windows.
/// </summary>
public sealed class DesktopPinService : IDesktopPinService
{
    private readonly ISettingsService _settings;
    private readonly object _sync = new();

    private IntPtr _taskbarCreatedMsgId = IntPtr.Zero;
    private readonly List<Window> _pinnedWindows = new();
    private readonly Dictionary<IntPtr, HwndSourceHook> _hooks = new();
    private HwndSource? _messageSink;
    private bool _disposed;
    private bool _lastPinFallback;
    private DateTime _listeningStartedUtc = DateTime.MinValue;

    // ---------- Dynamic TOPMOST (foreground tracking) ----------
    //
    // The note must be ABOVE the desktop (so Win+D doesn't hide it) but
    // BELOW normal application windows (so they can cover it). We can't
    // achieve both with a single static Z-order position, so we toggle:
    //   - Desktop (Progman/WorkerW) is foreground  => HWND_TOPMOST
    //   - Any other window is foreground           => HWND_NOTOPMOST
    //
    // The state is tracked via EVENT_SYSTEM_FOREGROUND, delivered on the UI
    // thread (WINEVENT_OUTOFCONTEXT). The delegate MUST be kept alive for the
    // lifetime of the hook, otherwise the GC will collect it and the hook
    // silently stops firing (a classic P/Invoke gotcha).
    private Win32.WinEventProc? _winEventProc;
    private IntPtr _foregroundHook = IntPtr.Zero;
    private bool _isDesktopForeground;

    public bool IsMounted
    {
        get
        {
            lock (_sync) { return _pinnedWindows.Count > 0; }
        }
    }

    public bool IsLastPinFallback => _lastPinFallback;

    public event EventHandler? RemountRequired;

    public DesktopPinService(ISettingsService settings)
    {
        _settings = settings;
    }

    // ---------- Public API ----------

    public bool PinToDesktop(Window window)
    {
        if (window == null) return false;

        var hwnd = EnsureHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            Logger.Error("PinToDesktop: could not obtain window handle.");
            _lastPinFallback = true;
            SubscribeFallback(window);
            return false;
        }

        var settings = _settings.Current;

        // If the primary mechanism (WM_WINDOWPOSCHANGING hook) is disabled by
        // settings, fall back to Method B only.
        if (!settings.EnableMethodA)
        {
            Logger.Info("Primary pin mechanism (WM_WINDOWPOSCHANGING hook) disabled by settings. Using Method B fallback only.");
            _lastPinFallback = true;
            SubscribeFallback(window);
            lock (_sync)
            {
                if (!_pinnedWindows.Contains(window))
                    _pinnedWindows.Add(window);
            }
            return false;
        }

        lock (_sync)
        {
            // Primary mechanism: install a WM_WINDOWPOSCHANGING hook on the
            // window. This intercepts minimization/hiding at the Win32 level
            // before the window state changes, resulting in zero visual flash.
            //
            // This replaces the previous SetParent-to-WorkerW approach, which
            // is fundamentally incompatible with WPF: WPF's render target
            // (HwndTarget/DirectComposition) cannot span process boundaries,
            // so a WPF window reparented to Progman (owned by explorer.exe)
            // never paints its content — the window appears "visible" but
            // shows the desktop wallpaper through it.
            var installed = InstallHook(hwnd);

            if (!installed)
            {
                Logger.Warn($"PinToDesktop: hook installation failed for 0x{hwnd:X}. Falling back to Method B only.");
                _lastPinFallback = true;
                SubscribeFallback(window);
                if (!_pinnedWindows.Contains(window))
                    _pinnedWindows.Add(window);
                return false;
            }

            Logger.Info($"Window 0x{hwnd:X} pinned (WM_WINDOWPOSCHANGING hook installed).");
            _lastPinFallback = false;

            if (!_pinnedWindows.Contains(window))
                _pinnedWindows.Add(window);

            // Apply the current dynamic TOPMOST state. OnSourceInitialized no
            // longer sets TOPMOST unconditionally (that made the note always
            // on top of every window). Instead we set TOPMOST only when the
            // desktop is the foreground window, and NOTOPMOST otherwise — so
            // normal app windows can cover the note, but Win+D can't hide it.
            //
            // Use the WPF Topmost property (not raw SetWindowPos) so WindowChrome
            // doesn't reset WS_EX_TOPMOST on the next style update.
            SafeExec.Try(() =>
            {
                if (window.Topmost != _isDesktopForeground)
                    window.Topmost = _isDesktopForeground;
            });

            // Method B: also subscribe to StateChanged as a backup — in case
            // the hook misses a minimization path (e.g. direct ShowWindow call
            // from another process that bypasses WM_WINDOWPOSCHANGING).
            if (settings.EnableMethodB)
                SubscribeFallback(window);

            return true;
        }
    }

    public void UnpinFromDesktop(Window window)
    {
        if (window == null) return;

        lock (_sync)
        {
            _pinnedWindows.Remove(window);
        }

        UnsubscribeFallback(window);

        SafeExec.Try(() =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            RemoveHook(hwnd);
        });
    }

    public void RemountAll()
    {
        List<Window> snapshot;
        lock (_sync)
        {
            snapshot = _pinnedWindows.ToList();
        }

        Logger.Info($"RemountAll: {snapshot.Count} window(s).");

        foreach (var window in snapshot)
        {
            if (!window.IsLoaded)
            {
                Logger.Info("Skipping remount — window not loaded.");
                continue;
            }

            UnsubscribeFallback(window);
            PinToDesktop(window);
        }
    }

    public void StartSystemEventListening()
    {
        if (_messageSink != null) return;

        _taskbarCreatedMsgId = (IntPtr)Win32.RegisterWindowMessageW("TaskbarCreated");
        Logger.Info($"TaskbarCreated message id = 0x{_taskbarCreatedMsgId.ToInt64():X}");

        var parameters = new HwndSourceParameters("StickyNotesMessageSink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            PositionX = 0,
            PositionY = 0,
            ParentWindow = IntPtr.Zero
        };

        _messageSink = new HwndSource(parameters);
        _messageSink.AddHook(WndProc);

        _listeningStartedUtc = DateTime.UtcNow;

        // Install the foreground-window WinEvent hook. The delegate is stored
        // in a field so the GC doesn't collect it while the hook is active.
        // WINEVENT_OUTOFCONTEXT delivers callbacks on this (UI) thread's
        // message loop, so SetWindowPos calls below are thread-safe.
        _winEventProc = OnForegroundChanged;
        _foregroundHook = Win32.SetWinEventHook(
            WindowConstants.EVENT_SYSTEM_FOREGROUND,
            WindowConstants.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _winEventProc,
            0, 0,
            WindowConstants.WINEVENT_OUTOFCONTEXT | WindowConstants.WINEVENT_SKIPOWNPROCESS);

        if (_foregroundHook != IntPtr.Zero)
        {
            Logger.Info("Foreground WinEvent hook installed (dynamic TOPMOST enabled).");
            // Initialize _isDesktopForeground from the CURRENT foreground
            // window, since SetWinEventHook only fires on future changes.
            UpdateTopmostForCurrentForeground();
        }
        else
        {
            Logger.Warn("SetWinEventHook(EVENT_SYSTEM_FOREGROUND) failed. Notes will default to TOPMOST for Win+D safety.");
            // Fallback: assume desktop may be foreground and keep notes on top
            // so Win+D immunity is preserved at the cost of always-on-top.
            _isDesktopForeground = true;
            ApplyTopmostToAll(topmost: true, foregroundHwnd: IntPtr.Zero);
        }

        Logger.Info("System event listening started (hidden message sink created).");
    }

    // ---------- Internal: WM_WINDOWPOSCHANGING hook ----------

    private bool InstallHook(IntPtr hwnd)
    {
        if (_hooks.ContainsKey(hwnd)) return true; // already installed

        var source = HwndSource.FromHwnd(hwnd);
        if (source == null)
        {
            Logger.Error($"InstallHook: HwndSource.FromHwnd(0x{hwnd:X}) returned null.");
            return false;
        }

        HwndSourceHook hook = WindowPosChangingHook;
        source.AddHook(hook);
        _hooks[hwnd] = hook;
        return true;
    }

    private void RemoveHook(IntPtr hwnd)
    {
        if (!_hooks.TryGetValue(hwnd, out var hook)) return;

        var source = HwndSource.FromHwnd(hwnd);
        source?.RemoveHook(hook);
        _hooks.Remove(hwnd);
        Logger.Info($"WM_WINDOWPOSCHANGING hook removed for window 0x{hwnd:X}.");
    }

    /// <summary>
    /// Intercepts WM_WINDOWPOSCHANGING to:
    ///   - Block hiding (SWP_HIDEWINDOW) and minimization (SWP_STATECHANGED)
    ///     when Win+D / Win+M / "Show Desktop" tries to minimize all windows.
    ///   - Enforce HWND_TOPMOST Z-order when the desktop is the foreground
    ///     window. Win+D's Z-order shuffle sends WM_WINDOWPOSCHANGING with
    ///     hwndInsertAfter=HWND_NOTOPMOST (or HWND_BOTTOM), which CLEARS
    ///     WS_EX_TOPMOST. By intercepting and redirecting to HWND_TOPMOST,
    ///     we prevent the style from being stripped in the first place —
    ///     which is far more reliable than trying to re-apply it afterward.
    /// </summary>
    private IntPtr WindowPosChangingHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WindowConstants.WM_WINDOWPOSCHANGING && lParam != IntPtr.Zero)
        {
            var wp = Marshal.PtrToStructure<Win32.WINDOWPOS>(lParam);
            var modified = false;

            // Block hiding (SWP_HIDEWINDOW)
            if ((wp.flags & WindowConstants.SWP_HIDEWINDOW) != 0)
            {
                wp.flags &= ~WindowConstants.SWP_HIDEWINDOW;
                modified = true;
                Logger.Info("WM_WINDOWPOSCHANGING: blocked SWP_HIDEWINDOW.");
            }

            // Block minimization: SWP_STATECHANGED means the window state
            // (normal/minimized/maximized) is changing. If the window is
            // currently NOT minimized, the change is TO minimized — block it.
            if ((wp.flags & WindowConstants.SWP_STATECHANGED) != 0)
            {
                var currentStyle = Win32.GetWindowStyle(hwnd);
                if ((currentStyle & WindowConstants.WS_MINIMIZE) == 0)
                {
                    wp.flags &= ~WindowConstants.SWP_STATECHANGED;
                    modified = true;
                    Logger.Info("WM_WINDOWPOSCHANGING: blocked minimization (cleared SWP_STATECHANGED).");
                }
            }

            // Enforce TOPMOST Z-order when desktop is foreground.
            // Win+D sends Z-order changes that strip WS_EX_TOPMOST; we intercept
            // them here and redirect to HWND_TOPMOST. This is the ONLY reliable
            // way to keep WS_EX_TOPMOST set — re-applying it after the fact
            // doesn't work because Win+D keeps sending Z-order changes.
            if (_isDesktopForeground
                && (wp.flags & WindowConstants.SWP_NOZORDER) == 0
                && wp.hwndInsertAfter != WindowConstants.HWND_TOPMOST)
            {
                var oldInsertAfter = wp.hwndInsertAfter;
                wp.hwndInsertAfter = WindowConstants.HWND_TOPMOST;
                modified = true;
                Logger.Info($"WM_WINDOWPOSCHANGING: forced HWND_TOPMOST for 0x{hwnd:X} (was 0x{oldInsertAfter.ToInt64():X}).");
            }

            if (modified)
            {
                Marshal.StructureToPtr(wp, lParam, fDeleteOld: false);
            }
        }
        return IntPtr.Zero;
    }

    // ---------- Internal: Method B fallback ----------

    private void SubscribeFallback(Window window)
    {
        window.StateChanged -= OnFallbackStateChanged; // avoid double-subscription
        window.StateChanged += OnFallbackStateChanged;
    }

    private void UnsubscribeFallback(Window window)
    {
        window.StateChanged -= OnFallbackStateChanged;
    }

    private void OnFallbackStateChanged(object? sender, EventArgs e)
    {
        if (sender is not Window window) return;
        if (window.WindowState != WindowState.Minimized) return;

        Logger.Warn("Window was minimized despite WM_WINDOWPOSCHANGING hook. Restoring (Method B fallback).");

        // Restore on the next dispatcher cycle to avoid reentrancy
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                window.WindowState = WindowState.Normal;
            }
            catch (Exception ex)
            {
                Logger.Error("Fallback restore failed.", ex);
            }
        }), DispatcherPriority.Input);
    }

    // ---------- Internal: WndProc for system broadcasts ----------

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var isTaskbarCreated = _taskbarCreatedMsgId != IntPtr.Zero
            && msg == _taskbarCreatedMsgId.ToInt64();

        var isDisplayChange = msg == WindowConstants.WM_DISPLAYCHANGE;

        // Suppress the spurious WM_DISPLAYCHANGE that PerMonitorV2 DPI
        // initialization broadcasts in the first few seconds of startup.
        if (isDisplayChange
            && _listeningStartedUtc != DateTime.MinValue
            && (DateTime.UtcNow - _listeningStartedUtc).TotalSeconds < 3)
        {
            Logger.Info("WM_DISPLAYCHANGE ignored during startup grace period.");
            return IntPtr.Zero;
        }

        if (!isTaskbarCreated && !isDisplayChange)
            return IntPtr.Zero;

        var reason = isTaskbarCreated ? "TaskbarCreated" : "WM_DISPLAYCHANGE";
        Logger.Warn($"System event received ({reason}, msg=0x{msg:X}). Triggering re-pin.");

        RemountRequired?.Invoke(this, EventArgs.Empty);
        handled = true;
        return IntPtr.Zero;
    }

    // ---------- Internal: dynamic TOPMOST (foreground tracking) ----------

    /// <summary>
    /// WinEvent callback for EVENT_SYSTEM_FOREGROUND. Delivered on the UI
    /// thread (WINEVENT_OUTOFCONTEXT). When the foreground window changes,
    /// we check whether it is the desktop and toggle TOPMOST on all pinned
    /// notes accordingly.
    ///
    /// IMPORTANT: If a window from OUR OWN PROCESS becomes foreground (a note
    /// window, or a WPF ContextMenu/Popup spawned by a note), we SKIP the
    /// state change. Toggling TOPMOST off when the user interacts with a note
    /// causes two problems:
    ///   1. The note's Z-order drops, and the TextBox right-click context menu
    ///      (copy/paste) renders behind other windows — "点不到".
    ///   2. Unnecessary Z-order churn on every click. The note should keep its
    ///      current TOPMOST/NOTOPMOST state until the DESKTOP or ANOTHER APP
    ///      becomes foreground.
    /// Although SetWinEventHook uses WINEVENT_SKIPOWNPROCESS, that flag is
    /// unreliable for EVENT_SYSTEM_FOREGROUND in some Windows versions — the
    /// event is system-level and may still be delivered. The explicit process
    /// check here is a belt-and-suspenders guard.
    /// </summary>
    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventCode,
        IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime)
    {
        // Only act on window-level events (ignore sub-object notifications).
        if (idObject != WindowConstants.OBJID_WINDOW) return;
        if (hwnd == IntPtr.Zero) return;

        // Don't react to any window from our own process becoming foreground.
        // This covers both note windows (user clicked a note) AND WPF
        // ContextMenu/Popup windows (user right-clicked a TextBox for
        // copy/paste). In both cases, toggling TOPMOST would be wrong.
        if (IsOwnProcessWindow(hwnd)) return;

        var isDesktop = IsDesktopWindow(hwnd);
        UpdateTopmost(isDesktop, hwnd);
    }

    /// <summary>
    /// Reads the current foreground window and applies the matching TOPMOST
    /// state. Called once at hook installation (SetWinEventHook only fires on
    /// future changes, not for the current state).
    /// </summary>
    private void UpdateTopmostForCurrentForeground()
    {
        var fg = Win32.GetForegroundWindow();
        var isDesktop = IsDesktopWindow(fg);
        Logger.Info($"Initial foreground = 0x{fg.ToInt64():X} (class={Win32.GetClassName(fg)}), isDesktop={isDesktop}.");
        UpdateTopmost(isDesktop, fg);
    }

    /// <summary>
    /// If the desktop-foreground state changed, re-apply TOPMOST/NOTOPMOST on
    /// every pinned note window. Skips work (and avoids log spam) when the
    /// state is unchanged.
    /// </summary>
    private void UpdateTopmost(bool isDesktopForeground, IntPtr foregroundHwnd)
    {
        if (_isDesktopForeground == isDesktopForeground) return;
        _isDesktopForeground = isDesktopForeground;
        Logger.Info($"Foreground desktop state => {_isDesktopForeground} (fgHwnd=0x{foregroundHwnd.ToInt64():X}). Adjusting Z-order of all pinned notes.");

        ApplyTopmostToAll(_isDesktopForeground, foregroundHwnd);
    }

    private void ApplyTopmostToAll(bool topmost, IntPtr foregroundHwnd)
    {
        List<Window> snapshot;
        lock (_sync) { snapshot = _pinnedWindows.ToList(); }

        Logger.Info($"ApplyTopmostToAll(topmost={topmost}): processing {snapshot.Count} window(s).");

        foreach (var window in snapshot)
        {
            SafeExec.Try(() => EnforceTopmostOnWindow(window, topmost, foregroundHwnd));
        }
    }

    /// <summary>
    /// Enforces TOPMOST/NOTOPMOST on a single window.
    ///
    /// When topmost=true (desktop is foreground):
    ///   Sets HWND_TOPMOST + WS_EX_TOPMOST so the note stays visible above
    ///   desktop icons during Win+D.
    ///
    /// When topmost=false (other window is foreground):
    ///   Sets HWND_NOTOPMOST + clears WS_EX_TOPMOST, then explicitly places
    ///   the note BELOW the foreground window via SetWindowPos(note, fg, ...).
    ///   This is critical for layered windows (AllowsTransparency="True"):
    ///   layered windows render in a separate pass and can appear above
    ///   non-layered windows at the same Z-order level. Explicitly inserting
    ///   the note below the foreground window ensures it can be covered.
    /// </summary>
    private void EnforceTopmostOnWindow(Window window, bool topmost, IntPtr foregroundHwnd)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var wasTopmost = window.Topmost;
        const long WS_EX_TOPMOST = 0x8;

        // Step 1: WPF property (syncs internal flag, calls SetWindowPos)
        if (wasTopmost != topmost)
            window.Topmost = topmost;

        if (hwnd == IntPtr.Zero) return;

        if (topmost)
        {
            // TOPMOST: SetWindowPos(HWND_TOPMOST) places the note above all
            // non-topmost windows — desired when desktop is foreground.
            ForceTopmostStyle(hwnd, topmost: true, WS_EX_TOPMOST);
        }
        else
        {
            // NOTOPMOST: Do NOT call ForceTopmostStyle(false) — its
            // SetWindowPos(HWND_NOTOPMOST) places the note at the TOP of the
            // non-topmost band, ABOVE the foreground window. That defeats the
            // SetWindowPos(note, foreground) placement below. Instead, clear
            // only the style bit and place the note below the foreground.
            // This overcomes the layered-window rendering issue where
            // WS_EX_LAYERED notes appear above non-layered windows.
            ApplyNotTopmost(hwnd, WS_EX_TOPMOST, foregroundHwnd);
        }

        // Step 4: Delayed retry for race condition
        ScheduleTopmostReenforcement(hwnd, topmost, WS_EX_TOPMOST, remainingRetries: 4);

        var finalEx = Win32.GetWindowExStyle(hwnd);
        Logger.Info($"  window 0x{hwnd:X}: Topmost {wasTopmost}->{topmost}, EX=0x{finalEx:X}, WS_EX_TOPMOST={((finalEx & WS_EX_TOPMOST) != 0)}");
    }

    /// <summary>
    /// Updates Z-order and directly writes the WS_EX_TOPMOST style bit.
    /// Does NOT use SWP_FRAMECHANGED (which triggers WindowChrome to reset the style).
    /// </summary>
    private static void ForceTopmostStyle(IntPtr hwnd, bool topmost, long wsExTopmost)
    {
        // Update Z-order (NO SWP_FRAMECHANGED to avoid WindowChrome interference)
        Win32.SetWindowPos(hwnd,
            topmost ? WindowConstants.HWND_TOPMOST : WindowConstants.HWND_NOTOPMOST,
            0, 0, 0, 0,
            WindowConstants.SWP_NOMOVE | WindowConstants.SWP_NOSIZE | WindowConstants.SWP_NOACTIVATE);

        // Directly write the extended style bit (no window messages sent)
        var ex = Win32.GetWindowExStyle(hwnd);
        var target = topmost ? (ex | wsExTopmost) : (ex & ~wsExTopmost);
        if (ex != target)
            Win32.SetWindowExStyle(hwnd, target);
    }

    /// <summary>
    /// Schedules a delayed check to re-enforce WS_EX_TOPMOST if it was reset by
    /// Win+D's ongoing Z-order shuffle. Retries up to <paramref name="remainingRetries"/>
    /// times with 150ms intervals.
    ///
    /// RACE CONDITION GUARD: Win+D schedules re-enforcement timers with
    /// topmost=true. If the user Alt+Tabs back to a non-desktop window before
    /// the timers fire, the captured <paramref name="topmost"/> value is stale.
    /// Re-applying TOPMOST in that case would put the note above the
    /// foreground window — exactly the bug we are trying to fix. So each
    /// callback re-checks <see cref="_isDesktopForeground"/> (the CURRENT
    /// foreground state) and bails out if it no longer matches the value
    /// captured when the timer was scheduled.
    ///
    /// NOTOPMOST RE-ENFORCEMENT: When topmost=false, we must NOT call
    /// <see cref="ForceTopmostStyle"/> — it uses SetWindowPos(HWND_NOTOPMOST)
    /// which places the note at the TOP of the non-topmost band, ABOVE the
    /// foreground window. That would override the initial
    /// SetWindowPos(note, foreground) placement and put the note back on top
    /// of WeChat. Instead, we only clear the WS_EX_TOPMOST style bit (without
    /// touching Z-order) and re-place the note below the CURRENT foreground.
    /// </summary>
    private void ScheduleTopmostReenforcement(IntPtr hwnd, bool topmost, long wsExTopmost, int remainingRetries)
    {
        if (remainingRetries <= 0 || _disposed) return;

        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_disposed) return;

            // Re-check the CURRENT foreground state. If it changed since this
            // timer was scheduled (e.g. Win+D then Alt+Tab back to WeChat),
            // the captured 'topmost' is stale — applying it would re-assert
            // TOPMOST on top of the now-foreground app window. Bail out;
            // the foreground handler for the new state is responsible for
            // re-applying the correct style.
            var currentTopmost = _isDesktopForeground;
            if (currentTopmost != topmost)
            {
                Logger.Info($"  re-enforce skipped: foreground state changed {topmost}->{currentTopmost} for 0x{hwnd:X}.");
                return;
            }

            var ex = Win32.GetWindowExStyle(hwnd);
            var expected = topmost ? (ex | wsExTopmost) : (ex & ~wsExTopmost);
            if (ex != expected)
            {
                Logger.Info($"  re-enforce: 0x{hwnd:X} WS_EX_TOPMOST reset (EX=0x{ex:X}), re-applying (retry {5 - remainingRetries}/4).");
                if (topmost)
                {
                    // TOPMOST: SetWindowPos(HWND_TOPMOST) is correct — places
                    // the note above all non-topmost windows (desired).
                    ForceTopmostStyle(hwnd, topmost: true, wsExTopmost);
                }
                else
                {
                    // NOTOPMOST: Do NOT use ForceTopmostStyle — its
                    // SetWindowPos(HWND_NOTOPMOST) would place the note at the
                    // TOP of the non-topmost band, above the foreground window.
                    // Instead, only clear the style bit and re-place below the
                    // current foreground window.
                    ApplyNotTopmost(hwnd, wsExTopmost, IntPtr.Zero);
                }
                ScheduleTopmostReenforcement(hwnd, topmost, wsExTopmost, remainingRetries - 1);
            }
        };
        timer.Start();
    }

    /// <summary>
    /// Applies the NOTOPMOST state WITHOUT using SetWindowPos(HWND_NOTOPMOST).
    /// Only clears the WS_EX_TOPMOST style bit (no Z-order band reassignment),
    /// then explicitly places the note BELOW the foreground window. This
    /// prevents the note from jumping to the top of the non-topmost band
    /// (above the foreground) — the root cause of the "便签在最上面" bug.
    ///
    /// If <paramref name="foregroundHwnd"/> is IntPtr.Zero, the CURRENT
    /// foreground window (GetForegroundWindow) is used — needed in the
    /// re-enforcement timer where the foreground may have changed since
    /// the timer was scheduled.
    /// </summary>
    private void ApplyNotTopmost(IntPtr hwnd, long wsExTopmost, IntPtr foregroundHwnd)
    {
        var exBefore = Win32.GetWindowExStyle(hwnd);
        var fg = foregroundHwnd != IntPtr.Zero ? foregroundHwnd : Win32.GetForegroundWindow();
        var fgIsTopmost = fg != IntPtr.Zero && (Win32.GetWindowExStyle(fg) & wsExTopmost) != 0;

        Logger.Info($"  NOTOPMOST: 0x{hwnd:X} EX=0x{exBefore:X}, fg=0x{fg.ToInt64():X} fgTopmost={fgIsTopmost}.");

        if (fgIsTopmost)
        {
            // CASE A: Foreground window is itself TOPMOST (e.g. another always-on-top
            // app, or a window that Win+D left in the topmost band). We CANNOT use
            // SetWindowPos(note, fg) here — inserting the note after a topmost
            // window pulls it INTO the topmost band and re-adds WS_EX_TOPMOST,
            // defeating the whole purpose. Instead, move the note to the
            // non-topmost band via HWND_NOTOPMOST. The note will then be BELOW
            // all topmost windows (including fg), which is exactly what we want.
            Win32.SetWindowPos(hwnd, WindowConstants.HWND_NOTOPMOST, 0, 0, 0, 0,
                WindowConstants.SWP_NOMOVE | WindowConstants.SWP_NOSIZE | WindowConstants.SWP_NOACTIVATE);

            // SetWindowPos(HWND_NOTOPMOST) should clear WS_EX_TOPMOST, but WPF's
            // HwndSource may re-add it. Belt-and-suspenders: explicitly clear.
            var exAfter = Win32.GetWindowExStyle(hwnd);
            var target = exAfter & ~wsExTopmost;
            if (exAfter != target)
                Win32.SetWindowExStyle(hwnd, target);
        }
        else
        {
            // CASE B: Foreground is a normal (non-topmost) window. Clear the
            // WS_EX_TOPMOST style bit, then explicitly place the note BELOW the
            // foreground window. The SetWindowPos(note, fg) call is REQUIRED for
            // layered windows (AllowsTransparency="True" => WS_EX_LAYERED):
            // layered windows render in a separate pass and visually appear above
            // non-layered windows at the same Z-order level. Without explicitly
            // inserting the note below fg, it would visually stay on top despite
            // the Z-order change.
            var target = exBefore & ~wsExTopmost;
            if (exBefore != target)
                Win32.SetWindowExStyle(hwnd, target);

            if (fg != IntPtr.Zero && fg != hwnd)
            {
                SafeExec.Try(() =>
                {
                    Win32.SetWindowPos(hwnd, fg, 0, 0, 0, 0,
                        WindowConstants.SWP_NOMOVE | WindowConstants.SWP_NOSIZE | WindowConstants.SWP_NOACTIVATE);
                });
            }

            // Verify: if SetWindowPos(note, fg) somehow re-added WS_EX_TOPMOST
            // (shouldn't happen since fg is non-topmost), fall back to HWND_NOTOPMOST.
            var exAfter = Win32.GetWindowExStyle(hwnd);
            if ((exAfter & wsExTopmost) != 0)
            {
                Logger.Info($"  NOTOPMOST: WS_EX_TOPMOST re-added after placement — using HWND_NOTOPMOST fallback.");
                Win32.SetWindowPos(hwnd, WindowConstants.HWND_NOTOPMOST, 0, 0, 0, 0,
                    WindowConstants.SWP_NOMOVE | WindowConstants.SWP_NOSIZE | WindowConstants.SWP_NOACTIVATE);
            }
        }

        var exFinal = Win32.GetWindowExStyle(hwnd);
        Logger.Info($"  NOTOPMOST: final EX=0x{exFinal:X}, WS_EX_TOPMOST={((exFinal & wsExTopmost) != 0)}.");
    }

    /// <summary>
    /// Returns true if the given HWND is the desktop wallpaper layer — either
    /// Progman (hosts desktop icons) or WorkerW (paints wallpaper above icons).
    /// Both become foreground when the user presses Win+D or clicks the desktop.
    /// </summary>
    private static bool IsDesktopWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        var cls = Win32.GetClassName(hwnd);
        return cls == WindowConstants.ProgmanClass
            || cls == WindowConstants.WorkerWClass;
    }

    /// <summary>
    /// Returns true if the given HWND belongs to our own process. This covers
    /// both note windows AND WPF ContextMenu/Popup windows spawned by notes.
    /// Used to skip foreground-state changes when the user interacts with our
    /// own UI (clicking a note, right-clicking a TextBox for copy/paste) —
    /// we don't want these interactions to toggle TOPMOST on the notes.
    /// </summary>
    private static bool IsOwnProcessWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == Environment.ProcessId;
    }

    // ---------- Internal: handle acquisition ----------

    private static IntPtr EnsureHandle(Window window)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle != IntPtr.Zero) return helper.Handle;

        // Force HWND creation without showing the window
        helper.EnsureHandle();
        return helper.Handle;
    }

    // ---------- Dispose ----------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_sync)
        {
            foreach (var window in _pinnedWindows.ToList())
            {
                try { UnsubscribeFallback(window); } catch { }
                try
                {
                    var hwnd = new WindowInteropHelper(window).Handle;
                    if (hwnd != IntPtr.Zero) RemoveHook(hwnd);
                }
                catch { }
            }
            _pinnedWindows.Clear();
            _hooks.Clear();
        }

        // Remove the foreground WinEvent hook before releasing the delegate,
        // so no callback can fire after _winEventProc is nulled.
        if (_foregroundHook != IntPtr.Zero)
        {
            SafeExec.Try(() => Win32.UnhookWinEvent(_foregroundHook));
            _foregroundHook = IntPtr.Zero;
        }
        _winEventProc = null;

        _messageSink?.Dispose();
        _messageSink = null;
    }
}
