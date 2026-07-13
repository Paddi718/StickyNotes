using System.Diagnostics;

namespace StickyNotes.Utilities;

/// <summary>
/// Safe fire-and-forget helper. Never use bare `_ = Task.Run(...)` —
/// wrap with this to guarantee exceptions are logged.
/// </summary>
internal static class AsyncHelper
{
    public static async void FireAndForget(Func<Task> operation, string context)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected — not an error
        }
        catch (Exception ex)
        {
            Logger.Error($"[{context}] Background task failed: {ex.Message}", ex);
            Debug.WriteLine($"[{context}] {ex}");
        }
    }
}
