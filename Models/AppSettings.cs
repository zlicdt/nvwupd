namespace NvwUpd.Models;

public class AppSettings
{
    public int CheckIntervalHours { get; set; } = 24;
    
    /// <summary>
    /// Language code (e.g., "zh-CN", "en-US"). Empty string means system default.
    /// </summary>
    public string Language { get; set; } = "";
}
