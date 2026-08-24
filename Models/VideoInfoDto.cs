namespace VideoDownloader.Web.Models;

public class VideoInfoDto
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Thumbnail { get; set; } = string.Empty;

    public double DurationSeconds { get; set; }

    public string DirectUrl { get; set; } = string.Empty;

    public List<VideoFormatDto> AvailableFormats { get; set; } = new();
}
