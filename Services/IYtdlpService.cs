using VideoDownloader.Web.Models;

namespace VideoDownloader.Web.Services;

public interface IYtdlpService
{
    Task<VideoInfoDto?> GetVideoInfoAsync(string url);

    Task<string?> DownloadAsync(
        string url,
        string formatId);
}
