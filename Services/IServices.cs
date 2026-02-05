using NvwUpd.Models;

namespace NvwUpd.Services;

/// <summary>
/// Interface for Windows notification service.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows a toast notification for a new driver update.
    /// </summary>
    void ShowUpdateNotification(DriverInfo driverInfo, string currentVersion);

    /// <summary>
    /// Shows a simple notification.
    /// </summary>
    void ShowNotification(string title, string message);
}

/// <summary>
/// Interface for the update checker service.
/// </summary>
public interface IUpdateChecker
{
    /// <summary>
    /// Starts periodic update checks.
    /// </summary>
    void StartPeriodicCheck(TimeSpan interval);

    /// <summary>
    /// Stops periodic update checks.
    /// </summary>
    void StopPeriodicCheck();

    /// <summary>
    /// Manually triggers an update check.
    /// </summary>
    Task CheckForUpdateAsync();

    /// <summary>
    /// Event raised when a new update is found.
    /// </summary>
    event EventHandler<UpdateFoundEventArgs>? UpdateFound;
}

/// <summary>
/// Interface for app settings storage.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Current settings instance.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Loads settings from storage.
    /// </summary>
    Task<AppSettings> LoadAsync();

    /// <summary>
    /// Saves settings to storage.
    /// </summary>
    Task SaveAsync(AppSettings settings);
}

/// <summary>
/// Interface for tray icon service.
/// </summary>
public interface ITrayIconService
{
    /// <summary>
    /// Initializes tray icon and context menu.
    /// </summary>
    void Initialize();
}

/// <summary>
/// Event args for when an update is found.
/// </summary>
public class UpdateFoundEventArgs : EventArgs
{
    public required DriverInfo NewDriver { get; init; }
    public required GpuInfo GpuInfo { get; init; }
}
