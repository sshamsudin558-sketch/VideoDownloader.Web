namespace VideoDownloader.Web.Models;

public class VideoFormatDto
{
    public string FormatId { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public string Resolution { get; set; } = string.Empty;

    public string FileSizeFormatted { get; set; } = string.Empty;

    public bool IsAudioOnly { get; set; }
}
