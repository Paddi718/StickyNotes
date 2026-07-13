using System.IO;

namespace StickyNotes.Utilities;

/// <summary>
/// Centralized path computation for app data, local app data, and resources.
/// Notes/settings live under %AppData%\StickyNotes; logs under %LocalAppData%\StickyNotes.
/// </summary>
internal static class PathHelper
{
    private const string AppFolderName = "StickyNotes";

    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppFolderName);

    public static string LocalAppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName);

    public static string NotesFilePath => Path.Combine(AppDataDir, "notes.json");
    public static string SettingsFilePath => Path.Combine(AppDataDir, "settings.json");
    public static string LogFilePath => Path.Combine(LocalAppDataDir, "app.log");
    public static string CrashLogFilePath => Path.Combine(LocalAppDataDir, "crash.log");
    public static string RunningMarkerPath => Path.Combine(LocalAppDataDir, "running.marker");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(LocalAppDataDir);
    }
}
