using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using NvwUpd.Core;
using NvwUpd.Models;
using NvwUpd.Services;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

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
    private readonly IUpdateChecker _updateChecker;
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;

    private GpuInfo? _gpuInfo;
    private DriverInfo? _latestDriver;
    private CancellationTokenSource? _downloadCts;
    private bool _isDownloading;
    private bool _canResume;
    private DriverType _currentDriverType = DriverType.GameReady;
    private AppSettings _settings = new();
    private TrayIconManager? _trayIcon;
    private bool _isExplicitExit = false;

    public MainWindow()
    {
        InitializeComponent();

        // Set custom title bar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Set window size (compact but sufficient)
        var appWindow = AppWindow;
        appWindow.Resize(new SizeInt32(800, 600));

        // Get services
        _gpuDetector = App.Services.GetRequiredService<IGpuDetector>();
        _driverFetcher = App.Services.GetRequiredService<IDriverFetcher>();
        _driverDownloader = App.Services.GetRequiredService<IDriverDownloader>();
        _driverInstaller = App.Services.GetRequiredService<IDriverInstaller>();
        _updateChecker = App.Services.GetRequiredService<IUpdateChecker>();
        _startupService = App.Services.GetRequiredService<IStartupService>();
        _settingsService = App.Services.GetRequiredService<ISettingsService>();

        _trayIcon = new TrayIconManager(
            this,
            () => App.ShowMainWindow(),
            () => _updateChecker.CheckForUpdateAsync(),
            () =>
            {
                _isExplicitExit = true;
                _trayIcon?.Dispose();
                _trayIcon = null;
                App.Current.Exit();
            });

        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;

        // Initialize
        InitializeAsync();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_isExplicitExit)
        {
            args.Cancel = true;
            sender.Hide();
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
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

        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await _settingsService.LoadAsync();
        ApplyCheckInterval(_settings.CheckIntervalHours);
    }

    private void ApplyCheckInterval(int hours)
    {
        if (hours <= 0)
        {
            hours = 24;
        }

        _updateChecker.StartPeriodicCheck(TimeSpan.FromHours(hours));
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var numberBox = new NumberBox
        {
            Minimum = 1,
            Maximum = 168,
            Value = _settings.CheckIntervalHours,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        var startupCheckbox = new CheckBox
        {
            Content = "开机自启动",
            IsChecked = _startupService.IsEnabled
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "自动检查间隔（小时）" });
        panel.Children.Add(numberBox);
        panel.Children.Add(startupCheckbox);

        var dialog = new ContentDialog
        {
            Title = "设置",
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            Content = panel
        };

        if (Content is FrameworkElement root)
        {
            dialog.XamlRoot = root.XamlRoot;
        }

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var hours = (int)Math.Round(numberBox.Value);
        if (hours <= 0)
        {
            hours = 24;
        }

        _settings.CheckIntervalHours = hours;
        await _settingsService.SaveAsync(_settings);
        ApplyCheckInterval(hours);
        
        if (startupCheckbox.IsChecked.HasValue)
        {
            try
            {
                _startupService.SetEnabled(startupCheckbox.IsChecked.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Settings] Failed to set startup: {ex.Message}");
            }
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

        _currentDriverType = GameReadyRadio.IsChecked == true
            ? DriverType.GameReady
            : DriverType.Studio;

        await StartDownloadAndInstallAsync();
    }

    private async void DownloadControlButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading)
        {
            _downloadCts?.Cancel();
            return;
        }

        if (_canResume)
        {
            await StartDownloadAndInstallAsync();
        }
    }

    private async Task StartDownloadAndInstallAsync()
    {
        if (_latestDriver == null || _gpuInfo == null) return;

        UpdateButton.IsEnabled = false;
        CheckUpdateButton.IsEnabled = false;
        DownloadControlButton.Visibility = Visibility.Visible;
        DownloadControlButton.Content = "中断下载";
        _canResume = false;
        _isDownloading = true;

        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();

        Console.WriteLine($"[MainWindow] Starting update, driver type: {_currentDriverType}");
        Console.WriteLine($"[MainWindow] Download URL: {_latestDriver.DownloadUrl}");

        // Show download progress panel
        DownloadProgressPanel.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 0;
        DownloadPercentText.Text = "0%";
        
        // Show expected download path
        var expectedPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "NvwUpd", "Downloads", 
            $"NVIDIA-Driver-{_latestDriver.Version}-{_currentDriverType}.exe");
        DownloadPathText.Text = expectedPath;

        var existingSize = 0L;
        if (System.IO.File.Exists(expectedPath))
        {
            existingSize = new System.IO.FileInfo(expectedPath).Length;
        }

        if (_latestDriver.FileSize > 0 && existingSize > 0 && existingSize < _latestDriver.FileSize)
        {
            var resumedProgress = (double)existingSize / _latestDriver.FileSize;
            DownloadProgressBar.Value = resumedProgress * 100;
            DownloadPercentText.Text = $"{resumedProgress:P0}";
            StatusText.Text = $"检测到已下载 {resumedProgress:P0}，准备继续下载...";
        }

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
            
            var downloadResult = await _driverDownloader.DownloadDriverAsync(
                _latestDriver,
                _currentDriverType,
                progress,
                _downloadCts.Token);
            Console.WriteLine($"[MainWindow] Downloaded to: {downloadResult.FilePath}");
            
            // Update path to actual downloaded location
            DownloadPathText.Text = downloadResult.FilePath;

            if (downloadResult.WasRestarted && existingSize > 0)
            {
                StatusText.Text = "服务器不支持断点续传，已重新下载";
            }

            StatusText.Text = "正在安装驱动...";
            Console.WriteLine("[MainWindow] Installing driver...");
            await _driverInstaller.InstallDriverAsync(downloadResult.FilePath);

            StatusText.Text = "驱动安装完成！建议重启计算机。";
            Console.WriteLine("[MainWindow] Installation complete!");

            _canResume = false;
            DownloadControlButton.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) when (_downloadCts?.IsCancellationRequested == true)
        {
            StatusText.Text = "下载已中断，可继续下载";
            _canResume = true;
        }
        catch (HttpRequestException ex)
        {
            StatusText.Text = $"下载中断：{ex.Message}";
            _canResume = true;
        }
        catch (IOException ex)
        {
            StatusText.Text = $"下载中断：{ex.Message}";
            _canResume = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"更新失败: {ex.Message}";
            Console.WriteLine($"[MainWindow] Update failed: {ex.Message}");
            Console.WriteLine($"[MainWindow] Stack trace: {ex.StackTrace}");
            _canResume = false;
        }
        finally
        {
            _isDownloading = false;
            if (_canResume)
            {
                DownloadControlButton.Content = "继续下载";
                UpdateButton.IsEnabled = true;
                CheckUpdateButton.IsEnabled = true;
            }
            else
            {
                UpdateButton.IsEnabled = true;
                CheckUpdateButton.IsEnabled = true;
            }
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
