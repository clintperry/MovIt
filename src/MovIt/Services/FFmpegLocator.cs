namespace MovIt.Services;

/// <summary>Finds ffmpeg / ffprobe next to the app, in MovIt's private tools folder, on PATH, or in common install spots.</summary>
public static class FFmpegLocator
{
    private static readonly string ExeSuffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;

    /// <summary>Where MovIt installs its own copy of FFmpeg (%LOCALAPPDATA% on Windows, ~/.local/share on Linux, ~/Library/Application Support on macOS).</summary>
    public static string PrivateBinFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovIt", "ffmpeg", "bin");

    /// <summary>
    /// Folders to check beyond PATH. GUI apps launched from Finder/Dock don't inherit the shell's PATH,
    /// so Homebrew and MacPorts locations would otherwise be missed on macOS.
    /// </summary>
    private static readonly string[] WellKnownFolders = OperatingSystem.IsMacOS()
        ? ["/opt/homebrew/bin", "/usr/local/bin", "/opt/local/bin"]
        : OperatingSystem.IsLinux()
            ? ["/usr/bin", "/usr/local/bin", "/snap/bin"]
            : [];

    public static string? FindFFmpeg() => Find("ffmpeg" + ExeSuffix);

    public static string? FindFFprobe() => Find("ffprobe" + ExeSuffix);

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
            .. WellKnownFolders.Select(dir => Path.Combine(dir, exe)),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? SafeCombine(string dir, string file)
    {
        try { return Path.Combine(dir, file); }
        catch (ArgumentException) { return null; }
    }
}
