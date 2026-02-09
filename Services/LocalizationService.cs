using System.Globalization;
using System.Xml.Linq;

namespace NvwUpd.Services;

/// <summary>
/// Provides localized strings by loading .resw resource files based on the current culture.
/// </summary>
public class LocalizationService
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    private readonly Dictionary<string, string> _strings = new();

    public static LocalizationService Instance => _instance.Value;

    private LocalizationService()
    {
        LoadResources();
    }

    private void LoadResources()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        var basePath = Path.Combine(AppContext.BaseDirectory, "Strings");

        // Try exact match first (e.g., "zh-CN"), then language only (e.g., "zh"), then fallback to "en"
        var candidates = new[]
        {
            culture,
            culture.Split('-')[0],
            "en"
        };

        foreach (var candidate in candidates)
        {
            var reswPath = Path.Combine(basePath, candidate, "Resources.resw");
            if (File.Exists(reswPath))
            {
                LoadReswFile(reswPath);
                return;
            }
        }
    }

    private void LoadReswFile(string path)
    {
        try
        {
            var doc = XDocument.Load(path);
            foreach (var data in doc.Descendants("data"))
            {
                var name = data.Attribute("name")?.Value;
                var value = data.Element("value")?.Value;
                if (name != null && value != null)
                {
                    _strings[name] = value;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load resources from {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a localized string by its resource key.
    /// </summary>
    public string GetString(string key)
    {
        return _strings.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// Gets a localized format string and applies the given arguments.
    /// </summary>
    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }
}
