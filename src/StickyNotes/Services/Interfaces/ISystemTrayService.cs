namespace StickyNotes.Services.Interfaces;

/// <summary>
/// System tray icon with context menu for note management and app exit.
/// </summary>
public interface ISystemTrayService : IDisposable
{
    void Initialize();
}
