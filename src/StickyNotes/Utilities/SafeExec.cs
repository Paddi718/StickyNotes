using System.Runtime.CompilerServices;

namespace StickyNotes.Utilities;

/// <summary>
/// Safe execution wrappers that forbid empty catch blocks.
/// All exceptions are logged to the file logger; re-throw is opt-in.
/// </summary>
internal static class SafeExec
{
    public static void Try(Action action, [CallerMemberName] string? caller = null)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.Error($"[SafeExec:{caller}] {ex.Message}", ex);
        }
    }

    public static async Task TryAsync(Func<Task> action, [CallerMemberName] string? caller = null)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Logger.Error($"[SafeExec:{caller}] {ex.Message}", ex);
        }
    }

    public static T? Try<T>(Func<T> action, T? defaultValue = default, [CallerMemberName] string? caller = null)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            Logger.Error($"[SafeExec:{caller}] {ex.Message}", ex);
            return defaultValue;
        }
    }
}
