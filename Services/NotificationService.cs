using System.Runtime.InteropServices;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Extensions.DependencyInjection;
using NvwUpd.Models;

namespace NvwUpd.Services;

/// <summary>
/// Windows notification service using Windows App SDK.
/// </summary>
public class NotificationService : INotificationService, IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private bool _isInitialized;
    private readonly ILocalizationService _localizationService;

    public NotificationService()
    {
        _localizationService = App.Services.GetRequiredService<ILocalizationService>();
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            var notificationManager = AppNotificationManager.Default;
            notificationManager.NotificationInvoked += OnNotificationInvoked;
            notificationManager.Register();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification init failed: {ex.Message}");
        }
    }

    public void ShowUpdateNotification(DriverInfo driverInfo, string currentVersion)
    {
        if (!_isInitialized) return;

        try
        {
            var builder = new AppNotificationBuilder()
                .AddArgument("action", "viewUpdate")
                .AddText(_localizationService.GetString("NotificationUpdateAvailable"))
                .AddText(string.Format(_localizationService.GetString("NotificationNewVersion"), driverInfo.Version))
                .AddText(string.Format(_localizationService.GetString("NotificationCurrentVersion"), currentVersion))
                .AddButton(new AppNotificationButton(_localizationService.GetString("NotificationViewDetails"))
                    .AddArgument("action", "viewUpdate"))
                .AddButton(new AppNotificationButton(_localizationService.GetString("NotificationRemindLater"))
                    .AddArgument("action", "dismiss"));

            var notification = builder.BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Show notification failed: {ex.Message}");
        }
    }

    public void ShowNotification(string title, string message)
    {
        if (!_isInitialized) return;

        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message);

            var notification = builder.BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Show notification failed: {ex.Message}");
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        // Handle notification actions
        if (args.Arguments.TryGetValue("action", out var action))
        {
            if (string.Equals(action, "dismiss", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        // Default behavior: bring main window to foreground
        App.ShowMainWindow();
        if (App.MainWindow != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            SetForegroundWindow(hwnd);
        }
    }

    public void Dispose()
    {
        if (_isInitialized)
        {
            try
            {
                AppNotificationManager.Default.Unregister();
            }
            catch { }
        }
    }
}
