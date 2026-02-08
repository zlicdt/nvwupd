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
    private readonly ILocalizationService _localizationService;

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
        _localizationService = App.Services.GetRequiredService<ILocalizationService>();

        // Initialize localized UI text
        InitializeLocalizedUI();

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

    private void InitializeLocalizedUI()
    {
        Title = _localizationService.GetString("AppTitle");
        
        // Header
        MainTitleText.Text = _localizationService.GetString("MainTitle");
        SettingsButton.Content = _localizationService.GetString("SettingsButton");
        
        // GPU Info Card
        GpuInfoTitle.Text = _localizationService.GetString("GpuInfo");
        GpuLabel.Text = _localizationService.GetString("GpuLabel");
        CurrentVersionLabel.Text = _localizationService.GetString("CurrentVersionLabel");
        
        // Update Card
        NewVersionAvailableText.Text = _localizationService.GetString("NewVersionAvailable");
        LatestVersionLabel.Text = _localizationService.GetString("LatestVersionLabel");
        ReleaseDateLabel.Text = _localizationService.GetString("ReleaseDateLabel");
        SelectDriverTypeText.Text = _localizationService.GetString("SelectDriverType");
        GameReadyDriverText.Text = _localizationService.GetString("GameReadyDriver");
        GameReadyDescriptionText.Text = _localizationService.GetString("GameReadyDescription");
        StudioDriverText.Text = _localizationService.GetString("StudioDriver");
        StudioDescriptionText.Text = _localizationService.GetString("StudioDescription");
        
        // Download Progress
        DownloadProgressText.Text = _localizationService.GetString("DownloadProgress");
        
        // Buttons
        CheckUpdateButton.Content = _localizationService.GetString("CheckUpdate");
        UpdateButton.Content = _localizationService.GetString("UpdateNow");
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
        // Update UI with localized strings
        StatusText.Text = _localizationService.GetString("StatusDetecting");
        GpuNameText.Text = _localizationService.GetString("GpuDetecting");
        
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
                StatusText.Text = _localizationService.GetString("StatusLoaded");
            }
            else
            {
                Console.WriteLine("[MainWindow] No NVIDIA GPU detected");
                StatusText.Text = _localizationService.GetString("StatusNoGpu");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] Detection failed: {ex.Message}");
            StatusText.Text = string.Format(_localizationService.GetString("StatusDetectFailed"), ex.Message);
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
            Content = _localizationService.GetString("AutoStartLabel"),
            IsChecked = _startupService.IsEnabled
        };

        var currentLanguage = string.IsNullOrEmpty(_settings.Language) 
            ? _localizationService.GetCurrentLanguage() 
            : _settings.Language;
        
        var languageComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = currentLanguage.StartsWith("zh") ? 0 : 1
        };
        languageComboBox.Items.Add(_localizationService.GetString("LanguageZhCN"));
        languageComboBox.Items.Add(_localizationService.GetString("LanguageEnUS"));

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = _localizationService.GetString("CheckIntervalLabel") });
        panel.Children.Add(numberBox);
        panel.Children.Add(startupCheckbox);
        panel.Children.Add(new TextBlock { Text = _localizationService.GetString("LanguageLabel"), Margin = new Thickness(0, 12, 0, 0) });
        panel.Children.Add(languageComboBox);

        var dialog = new ContentDialog
        {
            Title = _localizationService.GetString("SettingsTitle"),
            PrimaryButtonText = _localizationService.GetString("SaveButton"),
            CloseButtonText = _localizationService.GetString("CancelButton"),
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
        
        // Handle language change
        var newLanguage = languageComboBox.SelectedIndex == 0 ? "zh-CN" : "en-US";
        var languageChanged = _settings.Language != newLanguage && 
                             (_settings.Language != "" || newLanguage != currentLanguage);
        _settings.Language = newLanguage;
        
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
        
        // Show restart message if language changed
        if (languageChanged)
        {
            var restartDialog = new ContentDialog
            {
                Title = _localizationService.GetString("SettingsTitle"),
                Content = _localizationService.GetString("RestartRequired"),
                CloseButtonText = "OK"
            };
            
            if (Content is FrameworkElement rootElement)
            {
                restartDialog.XamlRoot = rootElement.XamlRoot;
            }
            
            await restartDialog.ShowAsync();
        }
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_gpuInfo == null) return;

        CheckUpdateButton.IsEnabled = false;
        StatusText.Text = _localizationService.GetString("StatusCheckingUpdate");
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
                    StatusText.Text = _localizationService.GetString("StatusNewVersionFound");
                    Console.WriteLine($"[MainWindow] Update available: {_gpuInfo.DriverVersion} -> {_latestDriver.Version}");
                }
                else
                {
                    StatusText.Text = _localizationService.GetString("StatusAlreadyLatest");
                    UpdateCard.Visibility = Visibility.Collapsed;
                    Console.WriteLine("[MainWindow] Already up to date");
                }
            }
            else
            {
                StatusText.Text = _localizationService.GetString("StatusCannotGetDriver");
                Console.WriteLine("[MainWindow] Failed to get driver info (null response)");
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = string.Format(_localizationService.GetString("StatusCheckFailed"), ex.Message);
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
        DownloadControlButton.Content = _localizationService.GetString("StopDownload");
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
            StatusText.Text = string.Format(_localizationService.GetString("StatusResuming"), $"{resumedProgress:P0}");
        }

        try
        {
            StatusText.Text = _localizationService.GetString("StatusDownloading");
            Console.WriteLine("[MainWindow] Downloading driver...");
            
            var progress = new Progress<double>(p =>
            {
                DownloadProgressBar.Value = p * 100;
                DownloadPercentText.Text = $"{p:P0}";
                StatusText.Text = string.Format(_localizationService.GetString("StatusDownloadingProgress"), $"{p:P0}");
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
                StatusText.Text = _localizationService.GetString("StatusNoResume");
            }

            StatusText.Text = _localizationService.GetString("StatusInstalling");
            Console.WriteLine("[MainWindow] Installing driver...");
            await _driverInstaller.InstallDriverAsync(downloadResult.FilePath);

            StatusText.Text = _localizationService.GetString("StatusInstallComplete");
            Console.WriteLine("[MainWindow] Installation complete!");

            _canResume = false;
            DownloadControlButton.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) when (_downloadCts?.IsCancellationRequested == true)
        {
            StatusText.Text = _localizationService.GetString("StatusDownloadStopped");
            _canResume = true;
        }
        catch (HttpRequestException ex)
        {
            StatusText.Text = string.Format(_localizationService.GetString("StatusDownloadInterrupted"), ex.Message);
            _canResume = true;
        }
        catch (IOException ex)
        {
            StatusText.Text = string.Format(_localizationService.GetString("StatusDownloadInterrupted"), ex.Message);
            _canResume = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = string.Format(_localizationService.GetString("StatusUpdateFailed"), ex.Message);
            Console.WriteLine($"[MainWindow] Update failed: {ex.Message}");
            Console.WriteLine($"[MainWindow] Stack trace: {ex.StackTrace}");
            _canResume = false;
        }
        finally
        {
            _isDownloading = false;
            if (_canResume)
            {
                DownloadControlButton.Content = _localizationService.GetString("ResumeDownload");
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
