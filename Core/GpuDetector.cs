using System.Management;
using NvwUpd.Models;

namespace NvwUpd.Core;

/// <summary>
/// Detects NVIDIA GPU information using WMI.
/// </summary>
public class GpuDetector : IGpuDetector
{
    private const string NvidiaVendorId = "10DE";

    public Task<GpuInfo?> DetectGpuAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_VideoController WHERE AdapterCompatibility LIKE '%NVIDIA%'");

                foreach (ManagementObject gpu in searcher.Get())
                {
                    var name = gpu["Name"]?.ToString();
                    var driverVersion = gpu["DriverVersion"]?.ToString();
                    var pnpDeviceId = gpu["PNPDeviceID"]?.ToString();

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(driverVersion))
                        continue;

                    // Parse device ID from PNP ID
                    // Format: PCI\VEN_10DE&DEV_2684&SUBSYS_...
                    string? deviceId = null;
                    string? vendorId = null;

                    if (!string.IsNullOrEmpty(pnpDeviceId))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(
                            pnpDeviceId, @"VEN_(\w+)&DEV_(\w+)");
                        if (match.Success)
                        {
                            vendorId = match.Groups[1].Value;
                            deviceId = match.Groups[2].Value;
                        }
                    }

                    // Convert Windows driver version format to NVIDIA format
                    // Windows: 31.0.15.6603 -> NVIDIA: 566.03
                    var nvidiaVersion = ConvertToNvidiaVersion(driverVersion);

                    // Detect if notebook
                    var isNotebook = name.Contains("Laptop", StringComparison.OrdinalIgnoreCase) ||
                                    name.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
                                    name.Contains("Max-Q", StringComparison.OrdinalIgnoreCase);

                    // Detect series
                    var series = DetectSeries(name);

                    return new GpuInfo
                    {
                        Name = name,
                        DriverVersion = nvidiaVersion,
                        DeviceId = deviceId,
                        VendorId = vendorId,
                        Series = series,
                        IsNotebook = isNotebook
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GPU detection failed: {ex.Message}");
            }

            return null;
        });
    }

    /// <summary>
    /// Converts Windows driver version to NVIDIA version format.
    /// </summary>
    /// <remarks>
    /// Windows format: 31.0.15.6603
    /// NVIDIA format: 566.03 (last 5 digits, split as xxx.xx)
    /// </remarks>
    private static string ConvertToNvidiaVersion(string windowsVersion)
    {
        try
        {
            // Get the last part of the version
            var parts = windowsVersion.Split('.');
            if (parts.Length >= 2)
            {
                // Combine last two parts and take last 5 digits
                var combined = parts[^2] + parts[^1];
                if (combined.Length >= 5)
                {
                    var last5 = combined[^5..];
                    return $"{last5[..3]}.{last5[3..]}";
                }
            }
        }
        catch
        {
            // Fall back to original version
        }

        return windowsVersion;
    }

    /// <summary>
    /// Detects the GPU series from the name.
    /// </summary>
    private static string? DetectSeries(string name)
    {
        if (name.Contains("RTX 50")) return "GeForce RTX 50 Series";
        if (name.Contains("RTX 40")) return "GeForce RTX 40 Series";
        if (name.Contains("RTX 30")) return "GeForce RTX 30 Series";
        if (name.Contains("RTX 20")) return "GeForce RTX 20 Series";
        if (name.Contains("GTX 16")) return "GeForce GTX 16 Series";
        if (name.Contains("GTX 10")) return "GeForce GTX 10 Series";
        if (name.Contains("Quadro")) return "Quadro";
        if (name.Contains("Tesla")) return "Tesla";

        return null;
    }
}
