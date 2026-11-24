using System.IO.Compression;
using System.Net.Http;

namespace MovIt.Services;

/// <summary>
/// Downloads a static Windows build of FFmpeg into MovIt's private tools folder.
/// On macOS and Linux FFmpeg comes from the system package manager instead; see <see cref="ManualInstallHint"/>.
/// </summary>
public sealed class FFmpegInstaller
{
    /// <summary>True where MovIt can download FFmpeg itself (Windows only).</summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>What to tell the user on platforms where we don't download FFmpeg.</summary>
    public static string ManualInstallHint =>
        OperatingSystem.IsMacOS() ? "Install it with Homebrew:  brew install ffmpeg"
        : OperatingSystem.IsLinux() ? "Install it with your package manager, e.g.  sudo apt install ffmpeg  /  sudo dnf install ffmpeg"
        : "Install FFmpeg and make sure it is on your PATH.";

    private static readonly string[] DownloadUrls =
    [
        "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
    ];

    private static readonly HttpClient Http = CreateClient();

    /// <param name="progress">Receives 0–1 while downloading, then null while extracting.</param>
    public async Task InstallAsync(IProgress<double?> progress, CancellationToken ct)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException(ManualInstallHint);

        var tempZip = Path.Combine(Path.GetTempPath(), $"movit-ffmpeg-{Guid.NewGuid():N}.zip");
        var tempDir = Path.Combine(Path.GetTempPath(), $"movit-ffmpeg-{Guid.NewGuid():N}");
        Exception? lastError = null;

        try
        {
            foreach (var url in DownloadUrls)
            {
                try
                {
                    await DownloadAsync(url, tempZip, progress, ct);
                    lastError = null;
                    break;
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException && !ct.IsCancellationRequested)
                {
                    lastError = ex;
                }
            }

            if (lastError is not null)
                throw new InvalidOperationException($"Couldn't download FFmpeg: {lastError.Message}", lastError);

            progress.Report(null);
            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(tempZip, tempDir, overwriteFiles: true);

                // The archives contain <build-name>/bin/ffmpeg.exe – locate it rather than assume the layout.
                var ffmpeg = Directory.EnumerateFiles(tempDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault()
                             ?? throw new InvalidOperationException("The FFmpeg download didn't contain ffmpeg.exe.");
                var binDir = Path.GetDirectoryName(ffmpeg)!;

                Directory.CreateDirectory(FFmpegLocator.PrivateBinFolder);
                foreach (var file in Directory.EnumerateFiles(binDir, "*.exe"))
                    File.Copy(file, Path.Combine(FFmpegLocator.PrivateBinFolder, Path.GetFileName(file)), overwrite: true);
            }, ct);
        }
        finally
        {
            TryDelete(() => File.Delete(tempZip));
            TryDelete(() => Directory.Delete(tempDir, recursive: true));
        }
    }

    private static async Task DownloadAsync(string url, string destination, IProgress<double?> progress, CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(destination);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;
            progress.Report(total is > 0 ? (double)readTotal / total.Value : null);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MovIt/1.0");
        return client;
    }

    private static void TryDelete(Action delete)
    {
        try { delete(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
