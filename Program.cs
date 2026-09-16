using VideoDownloader.Web.Services;

Environment.SetEnvironmentVariable(
    "DOTNET_USE_POLLING_FILE_WATCHER",
    "1"
);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddScoped<IYtdlpService, YtdlpService>();

builder.Services.AddHostedService<DownloadCleanupService>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "VideoDownloader.Web API",
        Version = "v1",
        Description = "Video Downloader API"
    });
});

var app = builder.Build();

app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "VideoDownloader.Web v1"
    );

    options.RoutePrefix = "swagger";
});

app.UseStaticFiles();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] =
        "strict-origin-when-cross-origin";

    await next();
});

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();
