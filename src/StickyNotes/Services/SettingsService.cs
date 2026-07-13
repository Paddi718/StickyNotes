using Microsoft.Win32;
using StickyNotes.Models;
using StickyNotes.Services.Interfaces;
using StickyNotes.Utilities;

namespace StickyNotes.Services;

/// <summary>
/// Loads and persists app settings to %AppData%\StickyNotes\settings.json,
/// and manages Windows auto-start via the HKCU Run key.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "StickyNotes";
    private readonly object _sync = new();

    public AppSettings Current { get; private set; } = new();

    public SettingsService()
    {
        Load();
    }

    public void Load()
    {
        lock (_sync)
        {
            try
            {
                var loaded = JsonHelper.ReadFile<AppSettings>(PathHelper.SettingsFilePath);
                if (loaded != null)
                {
                    Current = loaded;
                    Logger.Info("Settings loaded.");
                }
                else
                {
                    Current = new AppSettings();
                    Logger.Info("No settings file found — using defaults.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Settings load failed: {ex.Message}", ex);
                Current = new AppSettings();
            }
        }
    }

    public void Reload()
    {
        Load();
    }

    public void Save()
    {
        lock (_sync)
        {
            JsonHelper.WriteFileAtomic(PathHelper.SettingsFilePath, Current);
        }
    }

    public void SetAutoStart(bool enabled)
    {
        SafeExec.Try(() =>
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null)
            {
                Logger.Warn($"Could not open Run registry key for writing: {RunKeyPath}");
                return;
            }

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                {
                    Logger.Warn("Could not determine current process path for auto-start.");
                    return;
                }
                key.SetValue(AppName, $"\"{exe}\"");
                Logger.Info($"Auto-start enabled: {exe}");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                Logger.Info("Auto-start disabled.");
            }
        });
    }

    public bool IsAutoStartEnabled()
    {
        return SafeExec.Try(() =>
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(AppName) != null;
        }, defaultValue: false);
    }
}
