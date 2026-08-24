namespace VideoDownloader.Web.Services;

public class DownloadCleanupService : BackgroundService
{
    private readonly ILogger<DownloadCleanupService> _logger;

    private readonly string _downloadsFolder;

    public DownloadCleanupService(
        ILogger<DownloadCleanupService> logger)
    {
        _logger = logger;

        _downloadsFolder =
            Path.Combine(
                AppContext.BaseDirectory,
                "Downloads"
            );
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                CleanupOldFiles();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Download cleanup failed."
                );
            }

            await Task.Delay(
                TimeSpan.FromMinutes(30),
                stoppingToken
            );
        }
    }

    private void CleanupOldFiles()
    {
        if (!Directory.Exists(_downloadsFolder))
            return;

        var folders =
            Directory.GetDirectories(
                _downloadsFolder
            );

        foreach (var folder in folders)
        {
            try
            {
                var folderInfo =
                    new DirectoryInfo(folder);

                if (
                    folderInfo.LastWriteTimeUtc <
                    DateTime.UtcNow.AddHours(-1)
                )
                {
                    Directory.Delete(
                        folder,
                        true
                    );

                    _logger.LogInformation(
                        "Deleted old download folder: {Folder}",
                        folder
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not delete folder: {Folder}",
                    folder
                );
            }
        }
    }
}
