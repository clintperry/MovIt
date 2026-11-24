using System.Diagnostics;
using System.Globalization;
using System.Text;
using MovIt.Models;

namespace MovIt.Services;

public sealed record ConversionOptions(ConversionPreset Preset, bool Deinterlace);

public sealed record ConversionResult(bool FellBackToReEncode);

public sealed class ConversionFailedException(string message) : Exception(message);

/// <summary>Runs ffmpeg to turn one input video into a .mov file, reporting progress as 0–1.</summary>
public sealed class FFmpegConverter(string ffmpegPath, string? ffprobePath)
{
    public async Task<ConversionResult> ConvertAsync(
        string input, string output, ConversionOptions options, IProgress<double> progress, CancellationToken ct)
    {
        var duration = await ProbeDurationAsync(input, ct);

        if (options.Preset.Kind == PresetKind.Smart && !options.Deinterlace)
        {
            try
            {
                await RunFFmpegAsync(BuildArguments(input, output, PresetKind.Smart, deinterlace: false), duration, progress, ct);
                return new ConversionResult(FellBackToReEncode: false);
            }
            catch (ConversionFailedException)
            {
                // Typical cause: codecs the .mov container can't hold (VP9, Opus, subtitles…). Re-encode instead.
                TryDelete(output);
                progress.Report(0);
            }
        }

        var kind = options.Preset.Kind == PresetKind.Smart ? PresetKind.H264 : options.Preset.Kind;
        await RunFFmpegAsync(BuildArguments(input, output, kind, options.Deinterlace), duration, progress, ct);
        return new ConversionResult(FellBackToReEncode: options.Preset.Kind == PresetKind.Smart);
    }

    internal static IReadOnlyList<string> BuildArguments(string input, string output, PresetKind kind, bool deinterlace)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-loglevel", "error", "-i", input };

        if (kind == PresetKind.Smart)
        {
            args.AddRange(["-map", "0:v?", "-map", "0:a?", "-c", "copy"]);
        }
        else
        {
            args.AddRange(["-map", "0:v:0?", "-map", "0:a?"]);

            var filters = new List<string>();
            if (deinterlace) filters.Add("bwdif=mode=send_frame");

            switch (kind)
            {
                case PresetKind.H264:
                    filters.Add("format=yuv420p");
                    args.AddRange(["-c:v", "libx264", "-preset", "medium", "-crf", "18"]);
                    args.AddRange(["-c:a", "aac", "-b:a", "192k"]);
                    break;

                case PresetKind.Hevc:
                    filters.Add("format=yuv420p");
                    args.AddRange(["-c:v", "libx265", "-preset", "medium", "-crf", "22", "-tag:v", "hvc1", "-x265-params", "log-level=error"]);
                    args.AddRange(["-c:a", "aac", "-b:a", "192k"]);
                    break;

                case PresetKind.ProRes:
                    filters.Add("format=yuv422p10le");
                    args.AddRange(["-c:v", "prores_ks", "-profile:v", "3", "-vendor", "apl0"]);
                    args.AddRange(["-c:a", "pcm_s16le"]);
                    break;
            }

            args.AddRange(["-vf", string.Join(',', filters)]);
        }

        args.AddRange(["-movflags", "+faststart", "-f", "mov", "-progress", "pipe:1", "-nostats", output]);
        return args;
    }

    private async Task RunFFmpegAsync(IReadOnlyList<string> arguments, TimeSpan? duration, IProgress<double> progress, CancellationToken ct)
    {
        var psi = CreateStartInfo(ffmpegPath, arguments);
        using var process = new Process { StartInfo = psi };
        var errors = new StringBuilder();

        process.Start();

        var stderrTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(CancellationToken.None) is { } line)
            {
                if (errors.Length < 8_000) errors.AppendLine(line);
            }
        }, CancellationToken.None);

        var stdoutTask = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync(CancellationToken.None) is { } line)
            {
                // -progress emits key=value lines; out_time_us is the position reached so far.
                if (duration is { TotalMicroseconds: > 0 } d
                    && line.StartsWith("out_time_us=", StringComparison.Ordinal)
                    && long.TryParse(line.AsSpan(12), NumberStyles.Integer, CultureInfo.InvariantCulture, out var us))
                {
                    progress.Report(Math.Clamp(us / d.TotalMicroseconds, 0, 1));
                }
                else if (line == "progress=end")
                {
                    progress.Report(1);
                }
            }
        }, CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        await Task.WhenAll(stderrTask, stdoutTask);

        if (process.ExitCode != 0)
        {
            var message = errors.ToString().Trim();
            throw new ConversionFailedException(string.IsNullOrEmpty(message)
                ? $"FFmpeg exited with code {process.ExitCode}."
                : LastLines(message, 3));
        }
    }

    private async Task<TimeSpan?> ProbeDurationAsync(string input, CancellationToken ct)
    {
        if (ffprobePath is null) return null;

        var psi = CreateStartInfo(ffprobePath,
            ["-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", input]);

        try
        {
            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            _ = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return double.TryParse((await stdout).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null; // Progress becomes indeterminate; conversion still works.
        }
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, IEnumerable<string> arguments)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);
        return psi;
    }

    private static string LastLines(string text, int count) =>
        string.Join(Environment.NewLine, text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).TakeLast(count));

    internal static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
