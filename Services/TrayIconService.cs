using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using H.NotifyIcon;

namespace NvwUpd.Services;

public class TrayIconService : ITrayIconService, IDisposable
{
    private readonly IUpdateChecker _updateChecker;
    private TaskbarIcon? _taskbarIcon;

    public TrayIconService(IUpdateChecker updateChecker)
    {
        _updateChecker = updateChecker;
    }

    public void Initialize()
    {
        if (_taskbarIcon != null)
        {
            return;
        }

        var icon = new TaskbarIcon
        {
            ToolTipText = "NVIDIA Driver Updater",
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/nvidia.ico")),
            ContextMenuMode = ContextMenuMode.PopupMenu
        };

        var menu = new MenuFlyout();

        var openItem = new MenuFlyoutItem { Text = "打开主窗口" };
        openItem.Click += (_, _) => App.ShowMainWindow();

        var checkItem = new MenuFlyoutItem { Text = "检查更新" };
        checkItem.Click += async (_, _) => await CheckForUpdateAsync();

        var exitItem = new MenuFlyoutItem { Text = "退出" };
        exitItem.Click += (_, _) => ExitApplication();

        menu.Items.Add(openItem);
        menu.Items.Add(checkItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exitItem);

        icon.ContextFlyout = menu;
        icon.ForceCreate(true);

        _taskbarIcon = icon;
    }

    public void Dispose()
    {
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            await _updateChecker.CheckForUpdateAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Tray update check failed: {ex.Message}");
        }
    }

    private void ExitApplication()
    {
        Dispose();
        Application.Current.Exit();
    }
}
