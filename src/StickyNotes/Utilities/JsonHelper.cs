using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StickyNotes.Utilities;

/// <summary>
/// JSON serialization helpers with tolerant defaults (camelCase, indented,
/// enum-as-number for compact storage).
/// </summary>
internal static class JsonHelper
{
    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static T? Deserialize<T>(string json) =>
        string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, _readOptions);

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, _writeOptions);

    public static T? ReadFile<T>(string path)
    {
        try
        {
            if (!File.Exists(path)) return default;
            var json = File.ReadAllText(path);
            return Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to read JSON file {path}: {ex.Message}", ex);
            return default;
        }
    }

    /// <summary>
    /// Atomic write: serialize to a .tmp file then move over the target.
    /// Prevents corruption if the process is killed mid-write.
    /// </summary>
    public static bool WriteFileAtomic<T>(string path, T value)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = Serialize(value);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to write JSON file {path}: {ex.Message}", ex);
            return false;
        }
    }
}
