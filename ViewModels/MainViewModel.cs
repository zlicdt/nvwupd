using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvwUpd.Core;
using NvwUpd.Models;
using NvwUpd.Services;

namespace NvwUpd.ViewModels;

/// <summary>
/// ViewModel for the main window.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IGpuDetector _gpuDetector;
    private readonly IDriverFetcher _driverFetcher;
    private readonly IDriverDownloader _driverDownloader;
    private readonly IDriverInstaller _driverInstaller;

    [ObservableProperty]
    private GpuInfo? _gpuInfo;

    [ObservableProperty]
    private DriverInfo? _latestDriver;

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    private DriverType _selectedDriverType = DriverType.GameReady;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private string _downloadPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = LocalizationService.Instance.GetString("Initializing");

    public MainViewModel(
        IGpuDetector gpuDetector,
        IDriverFetcher driverFetcher,
        IDriverDownloader driverDownloader,
        IDriverInstaller driverInstaller)
    {
        _gpuDetector = gpuDetector;
        _driverFetcher = driverFetcher;
        _driverDownloader = driverDownloader;
        _driverInstaller = driverInstaller;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = LocalizationService.Instance.GetString("DetectingGpu");

        try
        {
            GpuInfo = await _gpuDetector.DetectGpuAsync();

            if (GpuInfo != null)
            {
                StatusMessage = LocalizationService.Instance.GetString("GpuDetected", GpuInfo.Name);
            }
            else
            {
                StatusMessage = LocalizationService.Instance.GetString("NoNvidiaGpuDetected");
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            StatusMessage = LocalizationService.Instance.GetString("GpuDetectionFailed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CheckForUpdateAsync()
    {
        if (GpuInfo == null) return;

        IsLoading = true;
        HasUpdate = false;
        StatusMessage = LocalizationService.Instance.GetString("CheckingForUpdates");

        try
        {
            LatestDriver = await _driverFetcher.GetLatestDriverAsync(GpuInfo);

            if (LatestDriver != null)
            {
                HasUpdate = IsNewerVersion(LatestDriver.Version, GpuInfo.DriverVersion);
                StatusMessage = HasUpdate 
                    ? LocalizationService.Instance.GetString("NewVersionFoundWithVersion", LatestDriver.Version) 
                    : LocalizationService.Instance.GetString("AlreadyLatest");
            }
            else
            {
                StatusMessage = LocalizationService.Instance.GetString("CannotGetDriverInfoShort");
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            StatusMessage = LocalizationService.Instance.GetString("CheckUpdateFailedShort");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task UpdateDriverAsync()
    {
        if (LatestDriver == null) return;

        IsLoading = true;
        IsDownloading = true;
        DownloadProgress = 0;
        DownloadPath = Path.Combine(Path.GetTempPath(), "NvwUpd", "Downloads", 
            $"NVIDIA-Driver-{LatestDriver.Version}-{SelectedDriverType}.exe");

        try
        {
            StatusMessage = LocalizationService.Instance.GetString("DownloadingDriver");
            
            var progress = new Progress<double>(p =>
            {
                DownloadProgress = p;
                StatusMessage = LocalizationService.Instance.GetString("Downloading", $"{p:P0}");
            });

            var downloadResult = await _driverDownloader.DownloadDriverAsync(
                LatestDriver, 
                SelectedDriverType, 
                progress,
                CancellationToken.None);

            DownloadPath = downloadResult.FilePath;

            StatusMessage = LocalizationService.Instance.GetString("InstallingDriver");
            await _driverInstaller.InstallDriverAsync(downloadResult.FilePath);

            StatusMessage = LocalizationService.Instance.GetString("InstallComplete");
            HasUpdate = false;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = LocalizationService.Instance.GetString("UserCancelled");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            StatusMessage = LocalizationService.Instance.GetString("UpdateFailedShort");
        }
        finally
        {
            IsLoading = false;
            IsDownloading = false;
        }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        if (Version.TryParse(latest, out var latestVer) && 
            Version.TryParse(current, out var currentVer))
        {
            return latestVer > currentVer;
        }
        return string.Compare(latest, current, StringComparison.Ordinal) > 0;
    }
}
