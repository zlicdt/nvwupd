using NvwUpd.Models;

namespace NvwUpd.Core;

/// <summary>
/// Interface for detecting GPU information.
/// </summary>
public interface IGpuDetector
{
    /// <summary>
    /// Detects NVIDIA GPU information from the system.
    /// </summary>
    /// <returns>GPU information or null if no NVIDIA GPU found.</returns>
    Task<GpuInfo?> DetectGpuAsync();
}

/// <summary>
/// Interface for fetching latest driver information.
/// </summary>
public interface IDriverFetcher
{
    /// <summary>
    /// Gets the latest driver information for the specified GPU (default: Game Ready).
    /// </summary>
    /// <param name="gpuInfo">GPU information.</param>
    /// <returns>Latest driver information.</returns>
    Task<DriverInfo?> GetLatestDriverAsync(GpuInfo gpuInfo);

    /// <summary>
    /// Gets the latest driver information for the specified GPU and driver type.
    /// </summary>
    /// <param name="gpuInfo">GPU information.</param>
    /// <param name="driverType">Type of driver (Game Ready or Studio).</param>
    /// <returns>Latest driver information.</returns>
    Task<DriverInfo?> GetLatestDriverAsync(GpuInfo gpuInfo, DriverType driverType);
}

/// <summary>
/// Interface for downloading drivers.
/// </summary>
public interface IDriverDownloader
{
    /// <summary>
    /// Downloads the specified driver.
    /// </summary>
    /// <param name="driverInfo">Driver information.</param>
    /// <param name="driverType">Type of driver to download.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <returns>Path to the downloaded file.</returns>
    Task<string> DownloadDriverAsync(
        DriverInfo driverInfo, 
        DriverType driverType,
        IProgress<double>? progress = null);
}

/// <summary>
/// Interface for installing drivers.
/// </summary>
public interface IDriverInstaller
{
    /// <summary>
    /// Installs a driver from the specified path.
    /// </summary>
    /// <param name="installerPath">Path to the installer.</param>
    /// <param name="silent">Whether to install silently.</param>
    Task InstallDriverAsync(string installerPath, bool silent = true);
}
