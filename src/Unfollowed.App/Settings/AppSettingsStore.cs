using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unfollowed.App.Settings;

public sealed class AppSettingsStore
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _serializerOptions;

    public AppSettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Unfollowed",
            "settings.json");
        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public AppSettings Load(AppSettings defaults)
    {
        if (!File.Exists(_settingsPath))
        {
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _serializerOptions);
            return settings ?? defaults;
        }
        catch
        {
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, _serializerOptions);
        File.WriteAllText(_settingsPath, json);
    }

    public void Reset()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }
}
