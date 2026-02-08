using Windows.ApplicationModel.Resources;

namespace NvwUpd.Services;

/// <summary>
/// Service for managing application localization.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Gets a localized string by key.
    /// </summary>
    string GetString(string key);
    
    /// <summary>
    /// Gets the current language code (e.g., "zh-CN", "en-US").
    /// </summary>
    string GetCurrentLanguage();
    
    /// <summary>
    /// Sets the application language.
    /// </summary>
    void SetLanguage(string languageCode);
}

/// <summary>
/// Implementation of localization service using Windows.ApplicationModel.Resources.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly ResourceLoader _resourceLoader;
    
    public LocalizationService()
    {
        _resourceLoader = new ResourceLoader();
    }
    
    public string GetString(string key)
    {
        try
        {
            var value = _resourceLoader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch
        {
            return key;
        }
    }
    
    public string GetCurrentLanguage()
    {
        var primaryLanguage = Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride;
        if (string.IsNullOrEmpty(primaryLanguage))
        {
            // Get system language
            primaryLanguage = Windows.Globalization.ApplicationLanguages.Languages[0];
        }
        return primaryLanguage;
    }
    
    public void SetLanguage(string languageCode)
    {
        Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = languageCode;
    }
}
