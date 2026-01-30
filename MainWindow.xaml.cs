using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NvwUpd.Core;
using NvwUpd.Models;

namespace NvwUpd;

/// <summary>
/// Main window for the NVIDIA Driver Updater application.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly IGpuDetector _gpuDetector;
    private readonly IDriverFetcher _driverFetcher;
    private readonly IDriverDownloader _driverDownloader;
    private readonly IDriverInstaller _driverInstaller;

    private GpuInfo? _gpuInfo;
    private DriverInfo? _latestDriver;

    public MainWindow()
    {
        InitializeComponent();

        // Set custom title bar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Get services
        _gpuDetector = App.Services.GetRequiredService<IGpuDetector>();
        _driverFetcher = App.Services.GetRequiredService<IDriverFetcher>();
        _driverDownloader = App.Services.GetRequiredService<IDriverDownloader>();
        _driverInstaller = App.Services.GetRequiredService<IDriverInstaller>();

        // Initialize
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        try
        {
            _gpuInfo = await _gpuDetector.DetectGpuAsync();
            
            if (_gpuInfo != null)
            {
                GpuNameText.Text = _gpuInfo.Name;
                CurrentVersionText.Text = _gpuInfo.DriverVersion;
                StatusText.Text = "GPU 信息已加载";
            }
            else
            {
                StatusText.Text = "未检测到 NVIDIA GPU";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"检测失败: {ex.Message}";
        }
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_gpuInfo == null) return;

        CheckUpdateButton.IsEnabled = false;
        StatusText.Text = "正在检查更新...";

        try
        {
            _latestDriver = await _driverFetcher.GetLatestDriverAsync(_gpuInfo);

            if (_latestDriver != null)
            {
                LatestVersionText.Text = _latestDriver.Version;
                ReleaseDateText.Text = _latestDriver.ReleaseDate.ToString("yyyy-MM-dd");

                if (IsNewerVersion(_latestDriver.Version, _gpuInfo.DriverVersion))
                {
                    UpdateCard.Visibility = Visibility.Visible;
                    UpdateButton.IsEnabled = true;
                    StatusText.Text = "发现新版本驱动！";
                }
                else
                {
                    StatusText.Text = "当前已是最新版本";
                    UpdateCard.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                StatusText.Text = "无法获取最新驱动信息";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"检查更新失败: {ex.Message}";
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_latestDriver == null || _gpuInfo == null) return;

        UpdateButton.IsEnabled = false;
        CheckUpdateButton.IsEnabled = false;

        var driverType = GameReadyRadio.IsChecked == true 
            ? DriverType.GameReady 
            : DriverType.Studio;

        try
        {
            StatusText.Text = "正在下载驱动...";
            var downloadPath = await _driverDownloader.DownloadDriverAsync(_latestDriver, driverType);

            StatusText.Text = "正在安装驱动...";
            await _driverInstaller.InstallDriverAsync(downloadPath);

            StatusText.Text = "驱动安装完成！建议重启计算机。";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"更新失败: {ex.Message}";
        }
        finally
        {
            UpdateButton.IsEnabled = true;
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        // Compare version strings (e.g., "572.16" > "566.03")
        if (Version.TryParse(latest, out var latestVer) && 
            Version.TryParse(current, out var currentVer))
        {
            return latestVer > currentVer;
        }
        return string.Compare(latest, current, StringComparison.Ordinal) > 0;
    }
}
