using StickyNotes.Models;

namespace StickyNotes.Services.Interfaces;

/// <summary>
/// Loads and persists application settings, and manages auto-start registration.
/// </summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    void Save();
    void Reload();

    /// <summary>Register or unregister the app in HKCU\...\Run for auto-start.</summary>
    void SetAutoStart(bool enabled);

    bool IsAutoStartEnabled();
}
