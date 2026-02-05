using System.Diagnostics;
using System.Text.Json;
using NvwUpd.Models;

namespace NvwUpd.Services;

public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private AppSettings _current = new();

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsPath = Path.Combine(appData, "NvwUpd", "settings.json");
    }

    public AppSettings Current => _current;

    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    _current = settings;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings load failed: {ex.Message}");
        }

        return _current;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        _current = settings ?? new AppSettings();

        var dir = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(_current, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
    }
}
