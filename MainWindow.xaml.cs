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
            Console.WriteLine("[MainWindow] Detecting GPU...");
            _gpuInfo = await _gpuDetector.DetectGpuAsync();
            
            if (_gpuInfo != null)
            {
                Console.WriteLine($"[MainWindow] GPU detected: {_gpuInfo.Name}");
                Console.WriteLine($"[MainWindow] Driver version: {_gpuInfo.DriverVersion}");
                Console.WriteLine($"[MainWindow] Is Notebook: {_gpuInfo.IsNotebook}");
                
                GpuNameText.Text = _gpuInfo.Name;
                CurrentVersionText.Text = _gpuInfo.DriverVersion;
                StatusText.Text = "GPU 信息已加载";
            }
            else
            {
                Console.WriteLine("[MainWindow] No NVIDIA GPU detected");
                StatusText.Text = "未检测到 NVIDIA GPU";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] Detection failed: {ex.Message}");
            StatusText.Text = $"检测失败: {ex.Message}";
        }
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_gpuInfo == null) return;

        CheckUpdateButton.IsEnabled = false;
        StatusText.Text = "正在检查更新...";
        Console.WriteLine("[MainWindow] Checking for updates...");

        try
        {
            _latestDriver = await _driverFetcher.GetLatestDriverAsync(_gpuInfo);

            if (_latestDriver != null)
            {
                Console.WriteLine($"[MainWindow] Latest driver found:");
                Console.WriteLine($"  Version: {_latestDriver.Version}");
                Console.WriteLine($"  Release Date: {_latestDriver.ReleaseDate}");
                Console.WriteLine($"  Download URL: {_latestDriver.DownloadUrl}");
                
                LatestVersionText.Text = _latestDriver.Version;
                ReleaseDateText.Text = _latestDriver.ReleaseDate.ToString("yyyy-MM-dd");

                if (IsNewerVersion(_latestDriver.Version, _gpuInfo.DriverVersion))
                {
                    UpdateCard.Visibility = Visibility.Visible;
                    UpdateButton.IsEnabled = true;
                    StatusText.Text = "发现新版本驱动！";
                    Console.WriteLine($"[MainWindow] Update available: {_gpuInfo.DriverVersion} -> {_latestDriver.Version}");
                }
                else
                {
                    StatusText.Text = "当前已是最新版本";
                    UpdateCard.Visibility = Visibility.Collapsed;
                    Console.WriteLine("[MainWindow] Already up to date");
                }
            }
            else
            {
                StatusText.Text = "无法获取最新驱动信息";
                Console.WriteLine("[MainWindow] Failed to get driver info (null response)");
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"检查更新失败: {ex.Message}";
            Console.WriteLine($"[MainWindow] Check update failed: {ex.Message}");
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

        Console.WriteLine($"[MainWindow] Starting update, driver type: {driverType}");
        Console.WriteLine($"[MainWindow] Download URL: {_latestDriver.DownloadUrl}");

        // Show download progress panel
        DownloadProgressPanel.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 0;
        DownloadPercentText.Text = "0%";
        
        // Show expected download path
        var expectedPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "NvwUpd", "Downloads", 
            $"NVIDIA-Driver-{_latestDriver.Version}-{driverType}.exe");
        DownloadPathText.Text = expectedPath;

        try
        {
            StatusText.Text = "正在下载驱动...";
            Console.WriteLine("[MainWindow] Downloading driver...");
            
            var progress = new Progress<double>(p =>
            {
                DownloadProgressBar.Value = p * 100;
                DownloadPercentText.Text = $"{p:P0}";
                StatusText.Text = $"正在下载... {p:P0}";
            });
            
            var downloadPath = await _driverDownloader.DownloadDriverAsync(_latestDriver, driverType, progress);
            Console.WriteLine($"[MainWindow] Downloaded to: {downloadPath}");
            
            // Update path to actual downloaded location
            DownloadPathText.Text = downloadPath;

            StatusText.Text = "正在安装驱动...";
            Console.WriteLine("[MainWindow] Installing driver...");
            await _driverInstaller.InstallDriverAsync(downloadPath);

            StatusText.Text = "驱动安装完成！建议重启计算机。";
            Console.WriteLine("[MainWindow] Installation complete!");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"更新失败: {ex.Message}";
            Console.WriteLine($"[MainWindow] Update failed: {ex.Message}");
            Console.WriteLine($"[MainWindow] Stack trace: {ex.StackTrace}");
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
