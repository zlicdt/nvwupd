# Localization Guide

## Overview

NvwUpd now supports multiple languages through a resource-based localization system. The application currently supports:

- **Chinese (Simplified)** - zh-CN
- **English** - en-US

## Changing Language

1. Click the **Settings** button in the main window
2. Select your preferred language from the **Language** dropdown
3. Click **Save**
4. Restart the application for the language change to take effect

The selected language will be saved and automatically applied when the application starts.

## Default Language

If no language is explicitly selected, the application will use the system default language. If the system language is not supported, it will fall back to Chinese (Simplified).

## For Developers

### Adding New Translations

To add translations for a new language:

1. Create a new folder under `Strings/` with the language code (e.g., `Strings/ja-JP/` for Japanese)
2. Copy `Resources.resw` from an existing language folder
3. Translate all the `<value>` elements in the file
4. Add the language to the settings dialog in `MainWindow.xaml.cs`

### Resource File Structure

Resource files are located in:
- `Strings/zh-CN/Resources.resw` - Chinese (Simplified)
- `Strings/en-US/Resources.resw` - English

Each resource file contains key-value pairs for all UI text in the application.

### Using Localized Strings in Code

```csharp
// Inject the localization service
private readonly ILocalizationService _localizationService;

// Get a localized string
var text = _localizationService.GetString("KeyName");

// Get a localized string with formatting
var text = string.Format(_localizationService.GetString("KeyName"), arg1, arg2);
```

### Localization Service

The `LocalizationService` class provides:
- `GetString(string key)` - Gets a localized string by key
- `GetCurrentLanguage()` - Gets the current language code
- `SetLanguage(string languageCode)` - Sets the application language

The service is registered as a singleton in the dependency injection container.

## Technical Details

The localization system uses:
- Windows Resource (`.resw`) files for string storage
- `Windows.ApplicationModel.Resources.ResourceLoader` for resource loading
- `Windows.Globalization.ApplicationLanguages` for language management
- Application settings to persist the user's language preference

## Limitations

- Language changes require application restart to take full effect
- Some dynamic content (like error messages from system APIs) may not be localized
- Date and number formatting follows the system locale settings
