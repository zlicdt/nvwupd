using System.Text.RegularExpressions;
using System.Xml.Linq;
using NvwUpd.Models;

namespace NvwUpd.Core;

/// <summary>
/// Fetches latest driver information from NVIDIA's official API.
/// Uses the same API endpoints as nvidia.com/Download
/// </summary>
public class DriverFetcher : IDriverFetcher
{
    private readonly HttpClient _httpClient;

    // NVIDIA's official API endpoints
    private const string LookupValueSearchUrl = "https://www.nvidia.com/Download/API/lookupValueSearch.aspx";
    private const string ProcessFindUrl = "https://www.nvidia.com/Download/processFind.aspx";
    private const string DownloadBaseUrl = "https://us.download.nvidia.com/Windows";

    // Driver type constants: dtcid=1 for Game Ready, dtcid=0 for Studio
    private const int GameReadyDriverType = 1;
    private const int StudioDriverType = 0;

    public DriverFetcher()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<DriverInfo?> GetLatestDriverAsync(GpuInfo gpuInfo)
    {
        return await GetLatestDriverAsync(gpuInfo, DriverType.GameReady);
    }

    public async Task<DriverInfo?> GetLatestDriverAsync(GpuInfo gpuInfo, DriverType driverType)
    {
        try
        {
            Console.WriteLine($"[DriverFetcher] Looking up driver for: {gpuInfo.Name}");

            // Step 1: Get product IDs from NVIDIA API (or use cached mapping)
            var (psid, pfid) = await GetProductIdsAsync(gpuInfo);
            var osid = await GetOsIdAsync();
            
            Console.WriteLine($"[DriverFetcher] Product IDs - psid: {psid}, pfid: {pfid}, osid: {osid}");

            // Step 2: Query processFind.aspx to get driver list
            var dtcid = driverType == DriverType.GameReady ? GameReadyDriverType : StudioDriverType;
            var requestUrl = $"{ProcessFindUrl}?dtcid={dtcid}&lang=en-us&lid=1&osid={osid}&pfid={pfid}&psid={psid}";
            
            Console.WriteLine($"[DriverFetcher] Fetching from: {requestUrl}");
            
            var html = await _httpClient.GetStringAsync(requestUrl);
            
            // Step 3: Parse the HTML response to extract driver info
            var driverInfo = ParseDriverListHtml(html, gpuInfo.IsNotebook, driverType);
            
            if (driverInfo != null)
            {
                Console.WriteLine($"[DriverFetcher] Found driver: {driverInfo.Version}");
                Console.WriteLine($"[DriverFetcher] Download URL: {driverInfo.DownloadUrl}");
            }
            else
            {
                Console.WriteLine("[DriverFetcher] No driver found in response");
            }

            return driverInfo;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DriverFetcher] Error: {ex.Message}");
            Console.WriteLine($"[DriverFetcher] Stack: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Get OS ID for Windows 11 (or 10)
    /// </summary>
    private async Task<string> GetOsIdAsync()
    {
        try
        {
            // TypeID=4 returns OS list
            var xml = await _httpClient.GetStringAsync($"{LookupValueSearchUrl}?TypeID=4");
            var doc = XDocument.Parse(xml);
            
            // Look for Windows 11
            var win11 = doc.Descendants("LookupValue")
                .FirstOrDefault(x => x.Element("Name")?.Value?.Contains("Windows 11") == true);
            
            if (win11 != null)
            {
                var value = win11.Element("Value")?.Value;
                Console.WriteLine($"[DriverFetcher] Found Windows 11 OS ID: {value}");
                return value ?? "135";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DriverFetcher] Failed to get OS ID: {ex.Message}");
        }
        
        // Default to Windows 11 64-bit
        return "135";
    }

    /// <summary>
    /// Get product series ID (psid) and product family ID (pfid) from NVIDIA API
    /// </summary>
    private async Task<(string psid, string pfid)> GetProductIdsAsync(GpuInfo gpuInfo)
    {
        var name = gpuInfo.Name;
        var isNotebook = IsNotebookGpu(name) || gpuInfo.IsNotebook;
        
        Console.WriteLine($"[DriverFetcher] GPU: {name}, IsNotebook: {isNotebook}");

        try
        {
            // Step 1: Get product series (psid) - TypeID=2
            var psid = await FindProductSeriesAsync(name, isNotebook);
            if (string.IsNullOrEmpty(psid))
            {
                Console.WriteLine("[DriverFetcher] Could not find product series, using defaults");
                return GetDefaultProductIds(name, isNotebook);
            }

            // Step 2: Get product family (pfid) - TypeID=3
            var pfid = await FindProductIdAsync(name, isNotebook);
            if (string.IsNullOrEmpty(pfid))
            {
                Console.WriteLine("[DriverFetcher] Could not find product ID, using defaults");
                return GetDefaultProductIds(name, isNotebook);
            }

            return (psid, pfid);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DriverFetcher] API lookup failed: {ex.Message}, using defaults");
            return GetDefaultProductIds(name, isNotebook);
        }
    }

    /// <summary>
    /// Find product series ID from NVIDIA API
    /// </summary>
    private async Task<string?> FindProductSeriesAsync(string gpuName, bool isNotebook)
    {
        var xml = await _httpClient.GetStringAsync($"{LookupValueSearchUrl}?TypeID=2");
        var doc = XDocument.Parse(xml);
        
        // Build search pattern based on GPU name
        var searchPattern = BuildSeriesSearchPattern(gpuName, isNotebook);
        Console.WriteLine($"[DriverFetcher] Searching for series: {searchPattern}");
        
        var series = doc.Descendants("LookupValue")
            .Where(x => 
            {
                var seriesName = x.Element("Name")?.Value ?? "";
                return seriesName.Contains("GeForce", StringComparison.OrdinalIgnoreCase) &&
                       Regex.IsMatch(seriesName, searchPattern, RegexOptions.IgnoreCase);
            })
            .FirstOrDefault();
        
        if (series != null)
        {
            var value = series.Element("Value")?.Value;
            var seriesName = series.Element("Name")?.Value;
            Console.WriteLine($"[DriverFetcher] Found series: {seriesName} (ID: {value})");
            return value;
        }
        
        return null;
    }

    /// <summary>
    /// Find product ID (pfid) from NVIDIA API
    /// </summary>
    private async Task<string?> FindProductIdAsync(string gpuName, bool isNotebook)
    {
        var xml = await _httpClient.GetStringAsync($"{LookupValueSearchUrl}?TypeID=3");
        var doc = XDocument.Parse(xml);
        
        // Clean up GPU name for matching
        var cleanName = gpuName
            .Replace("NVIDIA ", "")
            .Replace("GeForce ", "")
            .Trim();
        
        Console.WriteLine($"[DriverFetcher] Searching for product: {cleanName}");
        
        // Try exact match first
        var product = doc.Descendants("LookupValue")
            .FirstOrDefault(x => 
            {
                var productName = x.Element("Name")?.Value ?? "";
                return productName.Equals(cleanName, StringComparison.OrdinalIgnoreCase) ||
                       productName.Equals($"GeForce {cleanName}", StringComparison.OrdinalIgnoreCase) ||
                       productName.Equals(gpuName, StringComparison.OrdinalIgnoreCase);
            });
        
        // Try partial match if exact match fails
        if (product == null)
        {
            product = doc.Descendants("LookupValue")
                .FirstOrDefault(x => 
                {
                    var productName = x.Element("Name")?.Value ?? "";
                    return productName.Contains(cleanName, StringComparison.OrdinalIgnoreCase);
                });
        }
        
        if (product != null)
        {
            var value = product.Element("Value")?.Value;
            var productName = product.Element("Name")?.Value;
            Console.WriteLine($"[DriverFetcher] Found product: {productName} (ID: {value})");
            return value;
        }
        
        return null;
    }

    /// <summary>
    /// Build regex pattern to find the right product series
    /// </summary>
    private static string BuildSeriesSearchPattern(string gpuName, bool isNotebook)
    {
        var name = gpuName.ToUpperInvariant();
        var notebookSuffix = isNotebook ? ".*Notebook" : "";
        
        if (name.Contains("RTX 50")) return $"RTX\\s*50.*{notebookSuffix}";
        if (name.Contains("RTX 40")) return $"RTX\\s*40.*{notebookSuffix}";
        if (name.Contains("RTX 30")) return $"RTX\\s*30.*{notebookSuffix}";
        if (name.Contains("RTX 20")) return $"RTX\\s*20.*{notebookSuffix}";
        if (name.Contains("GTX 16")) return $"GTX\\s*16.*{notebookSuffix}";
        if (name.Contains("GTX 10")) return $"GTX\\s*10.*{notebookSuffix}";
        
        return $"GeForce.*{notebookSuffix}";
    }

    /// <summary>
    /// Parse the HTML response from processFind.aspx
    /// </summary>
    private DriverInfo? ParseDriverListHtml(string html, bool isNotebook, DriverType driverType)
    {
        try
        {
            // Extract driver ID from the first result
            // Pattern: driverResults.aspx/(\d+)/
            var driverIdMatch = Regex.Match(html, @"driverResults\.aspx/(\d+)/");
            if (!driverIdMatch.Success)
            {
                Console.WriteLine("[DriverFetcher] No driver ID found in response");
                return null;
            }
            var driverId = driverIdMatch.Groups[1].Value;
            Console.WriteLine($"[DriverFetcher] Found driver ID: {driverId}");

            // Extract version number
            // Pattern: <td class="gridItem">(\d+\.\d+)</td>
            var versionMatch = Regex.Match(html, @"<td class=""gridItem"">(\d+\.\d+)</td>");
            if (!versionMatch.Success)
            {
                Console.WriteLine("[DriverFetcher] No version found in response");
                return null;
            }
            var version = versionMatch.Groups[1].Value;
            Console.WriteLine($"[DriverFetcher] Found version: {version}");

            // Extract release date
            // Pattern: <td class="gridItem" nowrap>([^<]+)</td>
            var dateMatch = Regex.Match(html, @"<td class=""gridItem"" nowrap>([^<]+)</td>");
            var releaseDate = dateMatch.Success ? dateMatch.Groups[1].Value.Trim() : "";

            // Extract driver type name
            var driverTypeName = driverType == DriverType.GameReady ? "GeForce Game Ready Driver" : "NVIDIA Studio Driver";
            var typeMatch = Regex.Match(html, @"<td class=""gridItem driverName""><b><a[^>]*>([^<]+)</a>");
            if (typeMatch.Success)
            {
                driverTypeName = typeMatch.Groups[1].Value.Trim();
            }

            // Construct download URL
            // Format: https://us.download.nvidia.com/Windows/{version}/{version}-{type}-win10-win11-64bit-international-dch-whql.exe
            var driverVariant = isNotebook ? "notebook" : "desktop";
            var downloadUrl = $"{DownloadBaseUrl}/{version}/{version}-{driverVariant}-win10-win11-64bit-international-dch-whql.exe";

            return new DriverInfo
            {
                Version = version,
                DownloadUrl = downloadUrl,
                ReleaseDate = ParseReleaseDate(releaseDate),
                FileSize = 0, // Will be determined during download
                ReleaseNotes = $"{driverTypeName} - {releaseDate}",
                DriverId = driverId,
                DriverTypeName = driverTypeName,
                SupportedTypes = [driverType]
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DriverFetcher] Parse error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Check if GPU name indicates a notebook/laptop GPU
    /// </summary>
    private static bool IsNotebookGpu(string gpuName)
    {
        var name = gpuName.ToUpperInvariant();
        return name.Contains("LAPTOP") || 
               name.Contains("NOTEBOOK") || 
               name.Contains("MOBILE") || 
               name.Contains("MAX-Q");
    }

    /// <summary>
    /// Get default product IDs based on GPU name (fallback)
    /// </summary>
    private static (string psid, string pfid) GetDefaultProductIds(string gpuName, bool isNotebook)
    {
        var name = gpuName.ToUpperInvariant();

        // RTX 40 Series Notebooks
        if (isNotebook)
        {
            if (name.Contains("RTX 4090")) return ("129", "1000");
            if (name.Contains("RTX 4080")) return ("129", "1001");
            if (name.Contains("RTX 4070")) return ("129", "1002");
            if (name.Contains("RTX 4060")) return ("129", "1007");
            if (name.Contains("RTX 4050")) return ("129", "1004");
            // RTX 30 Series Notebooks
            if (name.Contains("RTX 3080")) return ("128", "909");
            if (name.Contains("RTX 3070")) return ("128", "910");
            if (name.Contains("RTX 3060")) return ("128", "911");
            if (name.Contains("RTX 3050")) return ("128", "928");
        }

        // RTX 50 Series Desktop
        if (name.Contains("RTX 5090")) return ("141", "1031");
        if (name.Contains("RTX 5080")) return ("141", "1032");
        if (name.Contains("RTX 5070")) return ("141", "1034");

        // RTX 40 Series Desktop
        if (name.Contains("RTX 4090")) return ("129", "987");
        if (name.Contains("RTX 4080")) return ("129", "988");
        if (name.Contains("RTX 4070")) return ("129", "990");
        if (name.Contains("RTX 4060")) return ("129", "992");

        // RTX 30 Series Desktop
        if (name.Contains("RTX 3090")) return ("127", "895");
        if (name.Contains("RTX 3080")) return ("127", "896");
        if (name.Contains("RTX 3070")) return ("127", "897");
        if (name.Contains("RTX 3060")) return ("127", "920");

        // Default fallback
        return ("129", "988");
    }

    private static DateTime ParseReleaseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return DateTime.Now;
        
        if (DateTime.TryParse(dateStr, out var date))
            return date;
        
        return DateTime.Now;
    }
}
