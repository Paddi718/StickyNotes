using System.Diagnostics;
using System.IO;

namespace StickyNotes.Utilities;

/// <summary>
/// Thread-safe file logger with 5MB rotation. Writes to %LocalAppData%\StickyNotes\app.log.
/// Also mirrors to Debug in DEBUG builds.
/// </summary>
internal static class Logger
{
    private static readonly object _lock = new();
    private static readonly string _logFilePath = PathHelper.LogFilePath;
    private const long MaxLogSizeBytes = 5 * 1024 * 1024; // 5 MB

    static Logger()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
            RotateIfNeeded();
        }
        catch
        {
            // Logger must never throw — best effort only
        }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message, Exception? ex = null) =>
        Log("ERROR", ex == null ? message : $"{message} | {ex}");

    private static void Log(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, line);
            }
            catch
            {
                // Swallow — logging must not crash the app
            }
        }

#if DEBUG
        Debug.Write(line);
#endif
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var fi = new FileInfo(_logFilePath);
            if (!fi.Exists || fi.Length < MaxLogSizeBytes) return;

            var backup = Path.Combine(Path.GetDirectoryName(_logFilePath)!, "app.log.old");
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(_logFilePath, backup);
        }
        catch
        {
            // Ignore rotation failures
        }
    }
}
