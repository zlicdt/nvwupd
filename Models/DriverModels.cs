namespace NvwUpd.Models;

/// <summary>
/// Represents GPU information detected from the system.
/// </summary>
public class GpuInfo
{
    /// <summary>
    /// GPU product name (e.g., "NVIDIA GeForce RTX 4080").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Currently installed driver version.
    /// </summary>
    public required string DriverVersion { get; init; }

    /// <summary>
    /// GPU device ID.
    /// </summary>
    public string? DeviceId { get; init; }

    /// <summary>
    /// GPU vendor ID (should be 10DE for NVIDIA).
    /// </summary>
    public string? VendorId { get; init; }

    /// <summary>
    /// GPU series (e.g., "GeForce RTX 40 Series").
    /// </summary>
    public string? Series { get; init; }

    /// <summary>
    /// Indicates if this is a notebook GPU.
    /// </summary>
    public bool IsNotebook { get; init; }
}

/// <summary>
/// Represents driver information from NVIDIA.
/// </summary>
public class DriverInfo
{
    /// <summary>
    /// Driver version string.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Download URL for the driver.
    /// </summary>
    public required string DownloadUrl { get; init; }

    /// <summary>
    /// Release date of the driver.
    /// </summary>
    public DateTime ReleaseDate { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Release notes or highlights.
    /// </summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>
    /// Supported driver types.
    /// </summary>
    public DriverType[] SupportedTypes { get; init; } = [DriverType.GameReady];
}

/// <summary>
/// NVIDIA driver types.
/// </summary>
public enum DriverType
{
    /// <summary>
    /// Game Ready Driver - optimized for latest games.
    /// </summary>
    GameReady,

    /// <summary>
    /// Studio Driver - optimized for creative applications.
    /// </summary>
    Studio
}
