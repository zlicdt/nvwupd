using NvwUpd.Core;
using NvwUpd.Models;

namespace NvwUpd.Services;

/// <summary>
/// Service that periodically checks for driver updates.
/// </summary>
public class UpdateChecker : IUpdateChecker, IDisposable
{
    private readonly IGpuDetector _gpuDetector;
    private readonly IDriverFetcher _driverFetcher;
    private readonly INotificationService _notificationService;
    
    private Timer? _timer;
    private GpuInfo? _cachedGpuInfo;
    private string? _lastNotifiedVersion;

    public event EventHandler<UpdateFoundEventArgs>? UpdateFound;

    public UpdateChecker(
        IGpuDetector gpuDetector,
        IDriverFetcher driverFetcher,
        INotificationService notificationService)
    {
        _gpuDetector = gpuDetector;
        _driverFetcher = driverFetcher;
        _notificationService = notificationService;
    }

    public void StartPeriodicCheck(TimeSpan interval)
    {
        StopPeriodicCheck();
        
        // Initial check after 1 minute, then periodic
        _timer = new Timer(
            async _ => await CheckForUpdateAsync(),
            null,
            TimeSpan.FromMinutes(1),
            interval);
    }

    public void StopPeriodicCheck()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public async Task CheckForUpdateAsync()
    {
        try
        {
            // Get GPU info (use cached if available)
            _cachedGpuInfo ??= await _gpuDetector.DetectGpuAsync();
            
            if (_cachedGpuInfo == null)
            {
                System.Diagnostics.Debug.WriteLine("No NVIDIA GPU detected");
                return;
            }

            // Fetch latest driver
            var latestDriver = await _driverFetcher.GetLatestDriverAsync(_cachedGpuInfo);
            
            if (latestDriver == null)
            {
                System.Diagnostics.Debug.WriteLine("Could not fetch latest driver info");
                return;
            }

            // Check if newer version
            if (IsNewerVersion(latestDriver.Version, _cachedGpuInfo.DriverVersion))
            {
                // Don't notify again for the same version
                if (_lastNotifiedVersion == latestDriver.Version)
                    return;

                _lastNotifiedVersion = latestDriver.Version;

                // Show notification
                _notificationService.ShowUpdateNotification(latestDriver, _cachedGpuInfo.DriverVersion);

                // Raise event
                UpdateFound?.Invoke(this, new UpdateFoundEventArgs
                {
                    NewDriver = latestDriver,
                    GpuInfo = _cachedGpuInfo
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
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

    public void Dispose()
    {
        StopPeriodicCheck();
    }
}
