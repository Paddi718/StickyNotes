using System.Runtime.InteropServices;
using StickyNotes.Utilities;

namespace StickyNotes.Native;

/// <summary>
/// Locates the correct WorkerW window to use as the reparenting target.
///
/// Desktop window hierarchy on Win10/11 (Z-order, top to bottom):
///   ...other top-level windows...
///   WorkerW (wallpaper render layer — paints ABOVE desktop icons)
///   Progman (contains SHELLDLL_DefView → SysListView32 desktop icons)
///
/// We send the undocumented 0x052C message to Progman to ensure the
/// wallpaper-rendering WorkerW exists, then enumerate top-level windows
/// to find the WorkerW that sits immediately ABOVE the window hosting
/// SHELLDLL_DefView.
///
/// NOTE: FindWindowEx(NULL, hwndDefViewHost, "WorkerW", NULL) returns the
/// WorkerW with LOWER Z-order (below the host). We need the one ABOVE,
/// so we track the last-seen WorkerW during enumeration (EnumWindows
/// goes top→bottom, so a WorkerW seen BEFORE the SHELLDLL_DefView host
/// is above it in Z-order).
/// </summary>
internal static class WorkerWFinder
{
    /// <summary>
    /// Finds the WorkerW window that renders above the desktop icons.
    /// Returns IntPtr.Zero if not found.
    /// </summary>
    public static IntPtr FindTargetWorkerW()
    {
        // Step 1: locate Progman
        var progman = Win32.FindWindowW(WindowConstants.ProgmanClass, null);
        Logger.Info($"FindWindowW(Progman) => 0x{progman.ToInt64():X}");

        if (progman == IntPtr.Zero)
        {
            // Fallback: enumerate to find a window of class "Progman"
            progman = FindTopLevelByClass(WindowConstants.ProgmanClass);
            Logger.Info($"EnumWindows fallback for Progman => 0x{progman.ToInt64():X}");
        }

        if (progman == IntPtr.Zero)
        {
            Logger.Error("Progman window not found. Is Explorer running?");
            return IntPtr.Zero;
        }

        // Step 2: force Progman to spawn/ensure the WorkerW pair exists
        var smResult = Win32.SendMessageTimeoutW(
            progman,
            WindowConstants.WM_SPAWN_WORKERW,
            IntPtr.Zero,
            IntPtr.Zero,
            WindowConstants.SMTO_NORMAL,
            2000,
            out var spawnResult);
        Logger.Info($"SendMessageTimeout(0x052C) => ret={smResult}, result=0x{spawnResult.ToInt64():X}, lastError={Marshal.GetLastWin32Error()}");

        // Step 3: enumerate top-level windows (top→bottom Z-order).
        // Track the most recently seen WorkerW; when we hit the window
        // hosting SHELLDLL_DefView, the last WorkerW is the one above it.
        IntPtr targetWorkerW = IntPtr.Zero;
        IntPtr lastSeenWorkerW = IntPtr.Zero;
        int workerWCount = 0;

        Win32.EnumWindows((hwnd, lParam) =>
        {
            var className = Win32.GetClassName(hwnd);

            if (className == WindowConstants.WorkerWClass)
            {
                lastSeenWorkerW = hwnd;
                workerWCount++;
                return true; // continue
            }

            // Does this top-level window contain SHELLDLL_DefView?
            var defView = Win32.FindWindowExW(
                hwnd, IntPtr.Zero, WindowConstants.ShellDefViewClass, null);

            if (defView != IntPtr.Zero)
            {
                Logger.Info($"SHELLDLL_DefView found under window 0x{hwnd.ToInt64():X} (class={className}).");

                if (lastSeenWorkerW != IntPtr.Zero)
                {
                    targetWorkerW = lastSeenWorkerW;
                    Logger.Info($"Using WorkerW 0x{targetWorkerW.ToInt64():X} (immediately above desktop icons).");
                }
                else
                {
                    // No WorkerW seen above — try the standard FindWindowEx approach
                    // (returns WorkerW below this window in Z-order)
                    var belowWorkerW = Win32.FindWindowExW(
                        IntPtr.Zero, hwnd, WindowConstants.WorkerWClass, null);
                    if (belowWorkerW != IntPtr.Zero)
                    {
                        targetWorkerW = belowWorkerW;
                        Logger.Info($"No WorkerW above; using WorkerW below: 0x{targetWorkerW.ToInt64():X}.");
                    }
                    else
                    {
                        Logger.Warn("No WorkerW found above or below SHELLDLL_DefView host.");
                    }
                }

                return false; // stop enumeration
            }

            return true; // continue
        }, IntPtr.Zero);

        Logger.Info($"EnumWindows summary: saw {workerWCount} WorkerW window(s).");

        if (targetWorkerW == IntPtr.Zero)
        {
            // Last resort: if there's exactly one WorkerW, use it
            Logger.Warn("Primary WorkerW search failed. Trying single-WorkerW fallback.");
        }

        return targetWorkerW;
    }

    /// <summary>
    /// Returns the Progman window handle, or IntPtr.Zero if not found.
    /// Used as an alternative mount target (below desktop icons).
    /// </summary>
    public static IntPtr FindProgman()
    {
        var progman = Win32.FindWindowW(WindowConstants.ProgmanClass, null);
        if (progman == IntPtr.Zero)
        {
            progman = FindTopLevelByClass(WindowConstants.ProgmanClass);
        }
        if (progman == IntPtr.Zero)
            Logger.Warn("Progman window not found (FindWindowW and EnumWindows both failed).");
        return progman;
    }

    private static IntPtr FindTopLevelByClass(string className)
    {
        IntPtr found = IntPtr.Zero;
        Win32.EnumWindows((hwnd, _) =>
        {
            if (Win32.GetClassName(hwnd) == className)
            {
                found = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
