using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvwUpd.Core;
using NvwUpd.Models;

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
    private string _statusMessage = "正在初始化...";

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
        StatusMessage = "正在检测 GPU...";

        try
        {
            GpuInfo = await _gpuDetector.DetectGpuAsync();

            if (GpuInfo != null)
            {
                StatusMessage = $"已检测到 {GpuInfo.Name}";
            }
            else
            {
                StatusMessage = "未检测到 NVIDIA GPU";
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            StatusMessage = "GPU 检测失败";
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
        StatusMessage = "正在检查更新...";

        try
        {
            LatestDriver = await _driverFetcher.GetLatestDriverAsync(GpuInfo);

            if (LatestDriver != null)
            {
                HasUpdate = IsNewerVersion(LatestDriver.Version, GpuInfo.DriverVersion);
                StatusMessage = HasUpdate 
                    ? $"发现新版本: {LatestDriver.Version}" 
                    : "当前已是最新版本";
            }
            else
            {
                StatusMessage = "无法获取驱动信息";
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            StatusMessage = "检查更新失败";
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
            StatusMessage = "正在下载驱动...";
            
            var progress = new Progress<double>(p =>
            {
                DownloadProgress = p;
                StatusMessage = $"正在下载... {p:P0}";
            });

            var installerPath = await _driverDownloader.DownloadDriverAsync(
                LatestDriver, 
                SelectedDriverType, 
                progress);
            
            DownloadPath = installerPath;

            StatusMessage = "正在安装驱动...";
            await _driverInstaller.InstallDriverAsync(installerPath);

            StatusMessage = "驱动安装完成！建议重启计算机。";
            HasUpdate = false;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "用户取消了安装";
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            StatusMessage = "更新失败";
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
