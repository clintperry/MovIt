using System.IO;

namespace MovIt.Services;

/// <summary>Finds ffmpeg.exe / ffprobe.exe next to the app, in MovIt's private tools folder, or on PATH.</summary>
public static class FFmpegLocator
{
    /// <summary>Where MovIt installs its own copy of FFmpeg.</summary>
    public static string PrivateBinFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovIt", "ffmpeg", "bin");

    public static string? FindFFmpeg() => Find("ffmpeg.exe");

    public static string? FindFFprobe() => Find("ffprobe.exe");

    private static string? Find(string exe)
    {
        IEnumerable<string> candidates =
        [
            Path.Combine(AppContext.BaseDirectory, exe),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg", exe),
            Path.Combine(PrivateBinFolder, exe),
            .. (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(dir => SafeCombine(dir.Trim('"'), exe))
                .OfType<string>(),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? SafeCombine(string dir, string file)
    {
        try { return Path.Combine(dir, file); }
        catch (ArgumentException) { return null; }
    }
}
