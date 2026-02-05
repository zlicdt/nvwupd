using System.Runtime.InteropServices;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
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

    public NotificationService()
    {
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
                .AddText("🎮 NVIDIA 驱动更新可用")
                .AddText($"发现新版本 {driverInfo.Version}")
                .AddText($"当前版本: {currentVersion}")
                .AddButton(new AppNotificationButton("查看详情")
                    .AddArgument("action", "viewUpdate"))
                .AddButton(new AppNotificationButton("稍后提醒")
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
            switch (action)
            {
                case "viewUpdate":
                    // Bring main window to foreground
                    App.ShowMainWindow();
                    if (App.MainWindow != null)
                    {
                        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                        SetForegroundWindow(hwnd);
                    }
                    break;

                case "dismiss":
                    // Do nothing, just dismiss
                    break;
            }
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
