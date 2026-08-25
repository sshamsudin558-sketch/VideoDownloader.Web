using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using VideoDownloader.Web.Models;

namespace VideoDownloader.Web.Services;

public class YtdlpService : IYtdlpService
{
    private readonly string _ytdlpPath;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly string? _denoPath;

    public YtdlpService(IConfiguration configuration)
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        if (isWindows)
        {
            string toolsFolder = Path.Combine(
                AppContext.BaseDirectory,
                "Tools");

            _ytdlpPath = Path.Combine(
                toolsFolder,
                "yt-dlp.exe");

            _ffmpegPath = Path.Combine(
                toolsFolder,
                "ffmpeg.exe");

            _ffprobePath = Path.Combine(
                toolsFolder,
                "ffprobe.exe");

            _denoPath = configuration["Tools:DenoPath"]
                ?? @"C:\Deno\deno.exe";
        }
        else
        {
            _ytdlpPath = "/usr/local/bin/yt-dlp";
            _ffmpegPath = "/usr/bin/ffmpeg";
            _ffprobePath = "/usr/bin/ffprobe";

            _denoPath = null;
        }
    }

    public async Task<VideoInfoDto?> GetVideoInfoAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        CheckTools();

        var startInfo = CreateProcess();
        AddCommonArguments(startInfo);

        startInfo.ArgumentList.Add("--dump-single-json");
        startInfo.ArgumentList.Add("--no-playlist");
        startInfo.ArgumentList.Add(url);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        Task<string> outputTask =
            process.StandardOutput.ReadToEndAsync();

        Task<string> errorTask =
            process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string output = await outputTask;
        string error = await errorTask;

        if (process.ExitCode != 0 ||
            string.IsNullOrWhiteSpace(output))
        {
            throw new Exception(
                $"yt-dlp info failed. ExitCode: {process.ExitCode}. Error: {error}");
        }

        try
        {
            using var document =
                JsonDocument.Parse(output);

            JsonElement root =
                document.RootElement;

            var result = new VideoInfoDto
            {
                Id = GetString(root, "id"),
                Title = GetString(
                    root,
                    "title",
                    "Unknown Title"),
                Thumbnail = GetString(
                    root,
                    "thumbnail"),
                DurationSeconds =
                    GetDouble(root, "duration"),
                DirectUrl = url,
                AvailableFormats =
                    new List<VideoFormatDto>()
            };

            if (root.TryGetProperty(
                    "formats",
                    out JsonElement formats) &&
                formats.ValueKind ==
                JsonValueKind.Array)
            {
                foreach (
                    JsonElement format
                    in formats.EnumerateArray())
                {
                    string formatId =
                        GetString(
                            format,
                            "format_id");

                    if (string.IsNullOrWhiteSpace(
                        formatId))
                    {
                        continue;
                    }

                    string extension =
                        GetString(format, "ext");

                    string videoCodec =
                        GetString(format, "vcodec");

                    string audioCodec =
                        GetString(format, "acodec");

                    string resolution =
                        GetString(format, "resolution");

                    int width =
                        GetInt(format, "width");

                    int height =
                        GetInt(format, "height");

                    bool isAudioOnly =
                        string.Equals(
                            videoCodec,
                            "none",
                            StringComparison.OrdinalIgnoreCase)
                        &&
                        !string.Equals(
                            audioCodec,
                            "none",
                            StringComparison.OrdinalIgnoreCase);

                    bool hasVideo =
                        !string.IsNullOrWhiteSpace(
                            videoCodec)
                        &&
                        !string.Equals(
                            videoCodec,
                            "none",
                            StringComparison.OrdinalIgnoreCase);

                    if (!isAudioOnly && !hasVideo)
                    {
                        continue;
                    }

                    if (!isAudioOnly &&
                        width <= 0 &&
                        height <= 0)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(
                        resolution))
                    {
                        resolution =
                            width > 0 && height > 0
                                ? $"{width}x{height}"
                                : "Unknown";
                    }

                    string displayResolution =
                        isAudioOnly
                            ? "Audio Only"
                            : resolution;

                    if (result.AvailableFormats.Any(
                        x => x.FormatId == formatId))
                    {
                        continue;
                    }

                    result.AvailableFormats.Add(
                        new VideoFormatDto
                        {
                            FormatId = formatId,
                            Extension = extension,
                            Resolution =
                                displayResolution,
                            IsAudioOnly =
                                isAudioOnly
                        });
                }
            }

            result.AvailableFormats =
                result.AvailableFormats
                    .OrderByDescending(
                        x => GetResolutionNumber(
                            x.Resolution))
                    .ThenBy(
                        x => x.IsAudioOnly)
                    .ToList();

            return result;
        }
        catch (JsonException ex)
        {
            throw new Exception(
                $"Could not read yt-dlp JSON response. {ex.Message}");
        }
    }

    public async Task<string?> DownloadAsync(
        string url,
        string formatId)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            string.IsNullOrWhiteSpace(formatId))
        {
            return null;
        }

        CheckTools();

        string downloadFolder =
            Path.Combine(
                AppContext.BaseDirectory,
                "Downloads");

        Directory.CreateDirectory(
            downloadFolder);

        string jobFolder =
            Path.Combine(
                downloadFolder,
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            jobFolder);

        string outputTemplate =
            Path.Combine(
                jobFolder,
                "%(title)s [%(id)s].%(ext)s");

        try
        {
            bool isAudioOnly =
                await IsAudioFormatAsync(
                    url,
                    formatId);

            var startInfo = CreateProcess();

            AddCommonArguments(startInfo);

            startInfo.ArgumentList.Add(
                "--no-playlist");

            startInfo.ArgumentList.Add(
                "--newline");

            startInfo.ArgumentList.Add(
                "--no-warnings");

            startInfo.ArgumentList.Add(
                "--restrict-filenames");

            startInfo.ArgumentList.Add(
                "--ffmpeg-location");

            startInfo.ArgumentList.Add(
                Path.GetDirectoryName(
                    _ffmpegPath)!);

            startInfo.ArgumentList.Add("-f");

            if (isAudioOnly)
            {
                startInfo.ArgumentList.Add(
                    formatId);
            }
            else
            {
                startInfo.ArgumentList.Add(
                    $"{formatId}+bestaudio/{formatId}");
            }

            startInfo.ArgumentList.Add(
                "--merge-output-format");

            startInfo.ArgumentList.Add("mp4");

            startInfo.ArgumentList.Add("-o");

            startInfo.ArgumentList.Add(
                outputTemplate);

            startInfo.ArgumentList.Add(url);

            using var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();

            Task<string> outputTask =
                process.StandardOutput.ReadToEndAsync();

            Task<string> errorTask =
                process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            string output =
                await outputTask;

            string error =
                await errorTask;

            if (process.ExitCode != 0)
            {
                throw new Exception(
                    $"yt-dlp Download Error. ExitCode: {process.ExitCode}. {error}");
            }

            string[] files =
                Directory.GetFiles(jobFolder);

            string? downloadedFile =
                files
                    .Where(file =>
                        !file.EndsWith(
                            ".part",
                            StringComparison.OrdinalIgnoreCase)
                        &&
                        !file.EndsWith(
                            ".ytdl",
                            StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(
                        file =>
                            new FileInfo(file).Length)
                    .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(
                    downloadedFile) ||
                !File.Exists(downloadedFile))
            {
                throw new Exception(
                    "yt-dlp completed, but the downloaded file was not found. Output: "
                    + output);
            }

            return downloadedFile;
        }
        catch
        {
            try
            {
                if (Directory.Exists(jobFolder))
                {
                    Directory.Delete(
                        jobFolder,
                        true);
                }
            }
            catch
            {
            }

            throw;
        }
    }

    private async Task<bool> IsAudioFormatAsync(
        string url,
        string formatId)
    {
        var startInfo = CreateProcess();

        AddCommonArguments(startInfo);

        startInfo.ArgumentList.Add(
            "--dump-single-json");

        startInfo.ArgumentList.Add(
            "--no-playlist");

        startInfo.ArgumentList.Add(url);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        Task<string> outputTask =
            process.StandardOutput.ReadToEndAsync();

        Task<string> errorTask =
            process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string output =
            await outputTask;

        string error =
            await errorTask;

        if (process.ExitCode != 0 ||
            string.IsNullOrWhiteSpace(output))
        {
            throw new Exception(
                $"Could not inspect selected format. ExitCode: {process.ExitCode}. Error: {error}");
        }

        using var document =
            JsonDocument.Parse(output);

        JsonElement root =
            document.RootElement;

        if (!root.TryGetProperty(
                "formats",
                out JsonElement formats) ||
            formats.ValueKind !=
            JsonValueKind.Array)
        {
            return false;
        }

        foreach (
            JsonElement format
            in formats.EnumerateArray())
        {
            string currentFormatId =
                GetString(
                    format,
                    "format_id");

            if (!string.Equals(
                currentFormatId,
                formatId,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string videoCodec =
                GetString(format, "vcodec");

            string audioCodec =
                GetString(format, "acodec");

            return
                string.Equals(
                    videoCodec,
                    "none",
                    StringComparison.OrdinalIgnoreCase)
                &&
                !string.Equals(
                    audioCodec,
                    "none",
                    StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private ProcessStartInfo CreateProcess()
    {
        return new ProcessStartInfo
        {
            FileName = _ytdlpPath,
            WorkingDirectory =
                Path.GetDirectoryName(
                    _ytdlpPath)
                ?? AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private void AddCommonArguments(
        ProcessStartInfo processInfo)
    {
        if (!string.IsNullOrWhiteSpace(
                _denoPath) &&
            File.Exists(_denoPath))
        {
            processInfo.ArgumentList.Add(
                "--js-runtimes");

            processInfo.ArgumentList.Add(
                $"deno:{_denoPath}");
        }

        processInfo.ArgumentList.Add(
            "--extractor-args");

        processInfo.ArgumentList.Add(
            "youtube:player_client=android");
    }

    private void CheckTools()
    {
        if (!File.Exists(_ytdlpPath))
        {
            throw new FileNotFoundException(
                $"yt-dlp was not found at: {_ytdlpPath}");
        }

        if (!File.Exists(_ffmpegPath))
        {
            throw new FileNotFoundException(
                $"ffmpeg was not found at: {_ffmpegPath}");
        }

        if (!File.Exists(_ffprobePath))
        {
            throw new FileNotFoundException(
                $"ffprobe was not found at: {_ffprobePath}");
        }
    }

    private static string GetString(
        JsonElement element,
        string property,
        string defaultValue = "")
    {
        if (!element.TryGetProperty(
                property,
                out JsonElement value))
        {
            return defaultValue;
        }

        if (value.ValueKind ==
            JsonValueKind.String)
        {
            return value.GetString()
                ?? defaultValue;
        }

        return defaultValue;
    }

    private static double GetDouble(
        JsonElement element,
        string property)
    {
        if (!element.TryGetProperty(
                property,
                out JsonElement value))
        {
            return 0;
        }

        if (value.ValueKind ==
                JsonValueKind.Number &&
            value.TryGetDouble(
                out double result))
        {
            return result;
        }

        return 0;
    }

    private static int GetInt(
        JsonElement element,
        string property)
    {
        if (!element.TryGetProperty(
                property,
                out JsonElement value))
        {
            return 0;
        }

        if (value.ValueKind ==
                JsonValueKind.Number &&
            value.TryGetInt32(
                out int result))
        {
            return result;
        }

        return 0;
    }

    private static int GetResolutionNumber(
        string? resolution)
    {
        if (string.IsNullOrWhiteSpace(
                resolution))
        {
            return 0;
        }

        string numbers =
            new string(
                resolution
                    .Where(char.IsDigit)
                    .ToArray());

        return int.TryParse(
            numbers,
            out int result)
            ? result
            : 0;
    }
}}
