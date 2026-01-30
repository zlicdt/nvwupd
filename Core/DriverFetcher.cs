using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvwUpd.Models;

namespace NvwUpd.Core;

/// <summary>
/// Fetches latest driver information from NVIDIA's API.
/// </summary>
public class DriverFetcher : IDriverFetcher
{
    private readonly HttpClient _httpClient;

    // NVIDIA's driver lookup API endpoint
    private const string DriverLookupUrl = 
        "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php";

    public DriverFetcher()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NvwUpd/1.0");
    }

    public async Task<DriverInfo?> GetLatestDriverAsync(GpuInfo gpuInfo)
    {
        try
        {
            // Build the lookup URL with parameters
            var queryParams = BuildQueryParams(gpuInfo);
            var requestUrl = $"{DriverLookupUrl}?{queryParams}";

            System.Diagnostics.Debug.WriteLine($"Fetching driver info from: {requestUrl}");

            var jsonString = await _httpClient.GetStringAsync(requestUrl);
            System.Diagnostics.Debug.WriteLine($"API Response (first 500 chars): {jsonString[..Math.Min(500, jsonString.Length)]}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var response = JsonSerializer.Deserialize<NvidiaDriverResponse>(jsonString, options);

            if (response?.IDS?.Count > 0)
            {
                var driver = response.IDS[0];
                var downloadInfo = driver.DownloadInfo;
                
                if (downloadInfo == null)
                {
                    System.Diagnostics.Debug.WriteLine("downloadInfo is null");
                    return null;
                }

                // Get download URL from nested downloadInfo
                var downloadUrl = downloadInfo.DownloadURL ?? string.Empty;
                var version = downloadInfo.Version ?? "Unknown";
                var releaseDate = downloadInfo.ReleaseDateTime;
                var fileSize = downloadInfo.DownloadURLFileSize;

                System.Diagnostics.Debug.WriteLine($"Found driver version: {version}, URL: {downloadUrl}");

                return new DriverInfo
                {
                    Version = version,
                    DownloadUrl = downloadUrl,
                    ReleaseDate = ParseReleaseDate(releaseDate),
                    FileSize = ParseFileSize(fileSize),
                    ReleaseNotes = downloadInfo.ReleaseNotes,
                    SupportedTypes = [DriverType.GameReady, DriverType.Studio]
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Driver fetch failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        return null;
    }

    private static string BuildQueryParams(GpuInfo gpuInfo)
    {
        // Determine parameters based on GPU info
        var osId = Environment.Is64BitOperatingSystem ? "57" : "56"; // Windows 10/11 64-bit or 32-bit
        var langId = "1033"; // English (US) - MUST be 1033, not 1
        
        // Get product series and type IDs
        var (psid, pfid) = GetProductIds(gpuInfo);

        var queryParts = new List<string>
        {
            "func=DriverManualLookup",
            $"psid={psid}",
            $"pfid={pfid}",
            $"osID={osId}",
            $"languageCode={langId}",
            "isWHQL=1",
            "dch=1", // DCH driver
            "sort1=0",
            "numberOfResults=1"
        };

        return string.Join("&", queryParts);
    }

    /// <summary>
    /// Gets product series ID (psid) and product family ID (pfid) for the GPU.
    /// </summary>
    private static (string psid, string pfid) GetProductIds(GpuInfo gpuInfo)
    {
        var name = gpuInfo.Name.ToUpperInvariant();
        var isNotebook = name.Contains("LAPTOP") || name.Contains("NOTEBOOK") || 
                         name.Contains("MOBILE") || name.Contains("MAX-Q") ||
                         gpuInfo.IsNotebook;

        Console.WriteLine($"[DriverFetcher] GPU Name: {gpuInfo.Name}, IsNotebook: {isNotebook}");

        // RTX 40 Series Notebooks (psid=130)
        if (isNotebook)
        {
            if (name.Contains("RTX 4090")) return ("130", "1000");
            if (name.Contains("RTX 4080")) return ("130", "1001");
            if (name.Contains("RTX 4070")) return ("130", "1002");
            if (name.Contains("RTX 4060")) return ("130", "1003");
            if (name.Contains("RTX 4050")) return ("130", "1004");
            // RTX 30 Series Notebooks
            if (name.Contains("RTX 3080 TI")) return ("128", "948");
            if (name.Contains("RTX 3080")) return ("128", "909");
            if (name.Contains("RTX 3070 TI")) return ("128", "949");
            if (name.Contains("RTX 3070")) return ("128", "910");
            if (name.Contains("RTX 3060")) return ("128", "911");
            if (name.Contains("RTX 3050 TI")) return ("128", "927");
            if (name.Contains("RTX 3050")) return ("128", "928");
        }

        // RTX 50 Series Desktop
        if (name.Contains("RTX 5090")) return ("141", "1031");
        if (name.Contains("RTX 5080")) return ("141", "1032");
        if (name.Contains("RTX 5070 TI")) return ("141", "1033");
        if (name.Contains("RTX 5070")) return ("141", "1034");

        // RTX 40 Series Desktop
        if (name.Contains("RTX 4090")) return ("129", "987");
        if (name.Contains("RTX 4080 SUPER")) return ("129", "1008");
        if (name.Contains("RTX 4080")) return ("129", "988");
        if (name.Contains("RTX 4070 TI SUPER")) return ("129", "1009");
        if (name.Contains("RTX 4070 TI")) return ("129", "989");
        if (name.Contains("RTX 4070 SUPER")) return ("129", "1007");
        if (name.Contains("RTX 4070")) return ("129", "990");
        if (name.Contains("RTX 4060 TI")) return ("129", "991");
        if (name.Contains("RTX 4060")) return ("129", "992");

        // RTX 30 Series Desktop
        if (name.Contains("RTX 3090 TI")) return ("127", "947");
        if (name.Contains("RTX 3090")) return ("127", "895");
        if (name.Contains("RTX 3080 TI")) return ("127", "939");
        if (name.Contains("RTX 3080")) return ("127", "896");
        if (name.Contains("RTX 3070 TI")) return ("127", "940");
        if (name.Contains("RTX 3070")) return ("127", "897");
        if (name.Contains("RTX 3060 TI")) return ("127", "919");
        if (name.Contains("RTX 3060")) return ("127", "920");

        // Default
        Console.WriteLine($"[DriverFetcher] WARNING: Unknown GPU, using default RTX 4080");
        return ("129", "988");
    }

    private static DateTime ParseReleaseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return DateTime.Now;
        
        if (DateTime.TryParse(dateStr, out var date))
            return date;
        
        return DateTime.Now;
    }

    private static long ParseFileSize(string? sizeStr)
    {
        if (string.IsNullOrEmpty(sizeStr)) return 0;
        
        // Parse sizes like "700 MB" or "1.2 GB"
        var parts = sizeStr.Split(' ');
        if (parts.Length >= 2 && double.TryParse(parts[0], out var value))
        {
            return parts[1].ToUpperInvariant() switch
            {
                "GB" => (long)(value * 1024 * 1024 * 1024),
                "MB" => (long)(value * 1024 * 1024),
                "KB" => (long)(value * 1024),
                _ => (long)value
            };
        }

        return 0;
    }
}

#region NVIDIA API Response Models

internal class NvidiaDriverResponse
{
    [JsonPropertyName("Success")]
    public string? Success { get; set; }

    [JsonPropertyName("IDS")]
    public List<NvidiaDriverInfo>? IDS { get; set; }
}

internal class NvidiaDriverInfo
{
    [JsonPropertyName("downloadInfo")]
    public DownloadInfo? DownloadInfo { get; set; }
}

internal class DownloadInfo
{
    [JsonPropertyName("Success")]
    public string? Success { get; set; }

    [JsonPropertyName("ID")]
    public string? ID { get; set; }

    [JsonPropertyName("Version")]
    public string? Version { get; set; }

    [JsonPropertyName("DownloadURL")]
    public string? DownloadURL { get; set; }

    [JsonPropertyName("ReleaseDateTime")]
    public string? ReleaseDateTime { get; set; }

    [JsonPropertyName("DownloadURLFileSize")]
    public string? DownloadURLFileSize { get; set; }

    [JsonPropertyName("ReleaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }
}

#endregion
