using StickyNotes.Utilities;

namespace StickyNotes.Services;

/// <summary>
/// Enforces single-instance semantics via a named Mutex.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Global\\StickyNotesSingleInstance";
    private Mutex? _mutex;
    private bool _ownsMutex;

    /// <summary>
    /// Attempts to acquire the single-instance mutex.
    /// Returns true if this is the first instance; false if another instance is running.
    /// </summary>
    public bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out _ownsMutex);
            if (!_ownsMutex)
            {
                Logger.Warn("Another instance is already running.");
                return false;
            }
            Logger.Info("Single-instance mutex acquired.");
            return true;
        }
        catch (Exception ex)
        {
            // Fall back to allowing the app to run if mutex creation fails
            // (e.g., permission issues) — better than blocking the user.
            Logger.Error($"Single-instance mutex acquisition failed: {ex.Message}", ex);
            return true;
        }
    }

    public void Dispose()
    {
        try
        {
            if (_mutex != null && _ownsMutex)
            {
                _mutex.ReleaseMutex();
            }
            _mutex?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error($"Mutex release failed: {ex.Message}", ex);
        }
    }
}
