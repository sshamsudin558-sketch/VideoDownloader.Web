using Microsoft.AspNetCore.Mvc;
using VideoDownloader.Web.Services;

namespace VideoDownloader.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DownloadController : ControllerBase
{
    private readonly IYtdlpService _ytdlpService;

    public DownloadController(IYtdlpService ytdlpService)
    {
        _ytdlpService = ytdlpService;
    }

    [HttpPost("info")]
    public async Task<IActionResult> GetInfo(
        [FromBody] DownloadRequestDto request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new
            {
                message = "Please enter a video URL."
            });
        }

        string url = request.Url.Trim();

        if (!IsValidUrl(url))
        {
            return BadRequest(new
            {
                message = "Please enter a valid HTTP or HTTPS video URL."
            });
        }

        try
        {
            var result = await _ytdlpService.GetVideoInfoAsync(url);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Video information was not found."
                });
            }

            return Ok(result);
        }
        catch (FileNotFoundException ex)
        {
            return StatusCode(500, new
            {
                message = "Downloader tools are missing.",
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Get video info error: {ex}");

            return StatusCode(500, new
            {
                message = "Could not retrieve video information.",
                error = ex.Message
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Download(
        [FromBody] DownloadFileRequestDto request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                message = "Download request is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new
            {
                message = "Video URL is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.FormatId))
        {
            return BadRequest(new
            {
                message = "Please select a download format."
            });
        }

        string url = request.Url.Trim();
        string formatId = request.FormatId.Trim();

        if (!IsValidUrl(url))
        {
            return BadRequest(new
            {
                message = "Please enter a valid HTTP or HTTPS video URL."
            });
        }

        string? filePath = null;

        try
        {
            filePath = await _ytdlpService.DownloadAsync(
                url,
                formatId);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return NotFound(new
                {
                    message = "Downloaded file was not created."
                });
            }

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new
                {
                    message = "Downloaded file was not found."
                });
            }

            string contentType = GetContentType(filePath);
            string fileName = Path.GetFileName(filePath);

            return PhysicalFile(
                filePath,
                contentType,
                fileName,
                enableRangeProcessing: true);
        }
        catch (FileNotFoundException ex)
        {
            return StatusCode(500, new
            {
                message = "Downloader tools are missing.",
                error = ex.Message
            });
        }
        catch (OperationCanceledException)
        {
            if (!string.IsNullOrWhiteSpace(filePath) &&
                System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                }
                catch
                {
                }
            }

            return StatusCode(499, new
            {
                message = "Download was cancelled."
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Download error: {ex}");

            if (!string.IsNullOrWhiteSpace(filePath) &&
                System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                }
                catch
                {
                }
            }

            return StatusCode(500, new
            {
                message = "Download failed.",
                error = ex.Message
            });
        }
    }

    private static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(
                url.Trim(),
                UriKind.Absolute,
                out Uri? uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttp ||
               uri.Scheme == Uri.UriSchemeHttps;
    }

    private static string GetContentType(string filePath)
    {
        string extension = Path.GetExtension(filePath)
            .ToLowerInvariant();

        return extension switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            ".m4v" => "video/x-m4v",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",

            ".m4a" => "audio/mp4",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            ".opus" => "audio/opus",

            _ => "application/octet-stream"
        };
    }
}

public class DownloadRequestDto
{
    public string Url { get; set; } = string.Empty;
}

public class DownloadFileRequestDto
{
    public string Url { get; set; } = string.Empty;

    public string FormatId { get; set; } = string.Empty;
}
