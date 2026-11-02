using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MovIt.Models;
using MovIt.Services;

namespace MovIt.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public static readonly string[] VideoExtensions =
    [
        ".mp4", ".m4v", ".mkv", ".avi", ".wmv", ".asf", ".webm", ".flv", ".f4v", ".mpg", ".mpeg", ".m2v",
        ".mts", ".m2ts", ".ts", ".vob", ".3gp", ".3g2", ".ogv", ".dv", ".mxf", ".divx", ".rm", ".rmvb", ".mov",
    ];

    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        Items.CollectionChanged += OnItemsChanged;
        RefreshFFmpegState();
    }

    public ObservableCollection<ConversionItemViewModel> Items { get; } = [];

    public IReadOnlyList<ConversionPreset> Presets => ConversionPreset.All;

    [ObservableProperty]
    public partial ConversionPreset SelectedPreset { get; set; } = ConversionPreset.All[0];

    /// <summary>Recommended for VHS / camcorder captures. Forces a re-encode.</summary>
    [ObservableProperty]
    public partial bool Deinterlace { get; set; }

    /// <summary>Null means "save next to each original".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputFolderText), nameof(HasCustomOutputFolder))]
    public partial string? OutputFolder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand), nameof(CancelCommand), nameof(ClearCommand),
        nameof(AddFilesCommand), nameof(ChooseOutputFolderCommand), nameof(InstallFFmpegCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    public partial bool IsFFmpegAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsInstallingFFmpeg { get; set; }

    [ObservableProperty]
    public partial double? FFmpegInstallProgress { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public bool IsIdle => !IsBusy;

    public bool HasItems => Items.Count > 0;

    public bool HasCustomOutputFolder => OutputFolder is not null;

    public string OutputFolderText => OutputFolder ?? "Same folder as each original";

    public string QueueSummary
    {
        get
        {
            var done = Items.Count(i => i.Status == ConversionStatus.Done);
            return Items.Count == 0 ? string.Empty : $"{done} of {Items.Count} converted";
        }
    }

    // ---------- Adding files ----------

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void AddFiles()
    {
        var patterns = string.Join(';', VideoExtensions.Select(e => "*" + e));
        var dialog = new OpenFileDialog
        {
            Title = "Choose videos to convert",
            Multiselect = true,
            Filter = $"Video files|{patterns}|All files|*.*",
        };

        if (dialog.ShowDialog() == true)
            AddPaths(dialog.FileNames);
    }

    /// <summary>Adds files (and video files inside dropped folders), skipping duplicates.</summary>
    public void AddPaths(IEnumerable<string> paths)
    {
        if (IsBusy) return;

        foreach (var path in paths.SelectMany(ExpandPath))
        {
            if (Items.Any(i => string.Equals(i.SourcePath, path, StringComparison.OrdinalIgnoreCase)))
                continue;
            Items.Add(new ConversionItemViewModel(path));
        }
    }

    private static IEnumerable<string> ExpandPath(string path)
    {
        if (File.Exists(path))
            return [path];

        if (Directory.Exists(path))
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Where(f => VideoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase);
        }

        return [];
    }

    [RelayCommand]
    private void Remove(ConversionItemViewModel? item)
    {
        if (item is null || item.IsActive) return;
        Items.Remove(item);
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Clear() => Items.Clear();

    // ---------- Output folder ----------

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void ChooseOutputFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Save converted videos to…" };
        if (OutputFolder is not null) dialog.InitialDirectory = OutputFolder;

        if (dialog.ShowDialog() == true)
            OutputFolder = dialog.FolderName;
    }

    [RelayCommand]
    private void ResetOutputFolder() => OutputFolder = null;

    // ---------- Conversion ----------

    private bool CanConvert() => !IsBusy && IsFFmpegAvailable && Items.Any(i => i.Status != ConversionStatus.Done);

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        var ffmpeg = FFmpegLocator.FindFFmpeg();
        if (ffmpeg is null)
        {
            RefreshFFmpegState();
            return;
        }

        var converter = new FFmpegConverter(ffmpeg, FFmpegLocator.FindFFprobe());
        var options = new ConversionOptions(SelectedPreset, Deinterlace);

        _cts = new CancellationTokenSource();
        IsBusy = true;

        try
        {
            foreach (var item in Items.Where(i => i.Status != ConversionStatus.Done).ToList())
            {
                if (_cts.IsCancellationRequested) break;
                await ConvertOneAsync(converter, item, options, _cts.Token);
                OnPropertyChanged(nameof(QueueSummary));
            }
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
            UpdateStatusText();
            ConvertCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task ConvertOneAsync(FFmpegConverter converter, ConversionItemViewModel item, ConversionOptions options, CancellationToken ct)
    {
        item.Reset();

        if (!File.Exists(item.SourcePath))
        {
            item.Status = ConversionStatus.Failed;
            item.Message = "The file no longer exists.";
            return;
        }

        string output;
        try
        {
            output = GetOutputPath(item.SourcePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            item.Status = ConversionStatus.Failed;
            item.Message = ex.Message;
            return;
        }

        item.OutputPath = output;
        item.Status = ConversionStatus.Converting;
        item.Message = options.Preset.Kind == PresetKind.Smart && !options.Deinterlace ? "Re-wrapping…" : "Encoding…";
        item.IsIndeterminate = true;

        var stopwatch = Stopwatch.StartNew();
        var progress = new Progress<double>(p =>
        {
            item.IsIndeterminate = false;
            item.Progress = p;
            item.Message = FormatProgress(p, stopwatch.Elapsed);
        });

        try
        {
            var result = await converter.ConvertAsync(item.SourcePath, output, options, progress, ct);
            item.Progress = 1;
            item.IsIndeterminate = false;
            item.Status = ConversionStatus.Done;
            item.Message = result.FellBackToReEncode
                ? $"Done in {FormatElapsed(stopwatch.Elapsed)} · re-encoded to H.264 (original codecs don't fit .mov)"
                : $"Done in {FormatElapsed(stopwatch.Elapsed)} → {Path.GetFileName(output)}";
        }
        catch (OperationCanceledException)
        {
            FFmpegConverter.TryDelete(output);
            item.IsIndeterminate = false;
            item.Status = ConversionStatus.Cancelled;
            item.Message = "Cancelled";
        }
        catch (Exception ex)
        {
            FFmpegConverter.TryDelete(output);
            item.IsIndeterminate = false;
            item.Status = ConversionStatus.Failed;
            item.Message = ex.Message;
        }
    }

    private bool CanCancel() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    /// <summary>Builds "name.mov" in the target folder without overwriting anything (including the source).</summary>
    private string GetOutputPath(string source)
    {
        var folder = OutputFolder ?? Path.GetDirectoryName(source)!;
        Directory.CreateDirectory(folder);

        var baseName = Path.GetFileNameWithoutExtension(source);
        var candidate = Path.Combine(folder, baseName + ".mov");

        for (var n = 1; File.Exists(candidate) || string.Equals(candidate, source, StringComparison.OrdinalIgnoreCase); n++)
            candidate = Path.Combine(folder, $"{baseName} ({n}).mov");

        return candidate;
    }

    private static string FormatProgress(double p, TimeSpan elapsed)
    {
        var text = $"{p:P0}";
        if (p is > 0.02 and < 1)
        {
            var remaining = TimeSpan.FromSeconds(elapsed.TotalSeconds / p * (1 - p));
            text += $" · about {FormatElapsed(remaining)} left";
        }
        return text;
    }

    private static string FormatElapsed(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m"
        : t.TotalMinutes >= 1 ? $"{t.Minutes}m {t.Seconds}s"
        : $"{Math.Max(1, t.Seconds)}s";

    // ---------- Opening results ----------

    [RelayCommand]
    private static void OpenOutput(ConversionItemViewModel? item)
    {
        if (item?.OutputPath is { } path && File.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    [RelayCommand]
    private static void Play(ConversionItemViewModel? item)
    {
        if (item?.OutputPath is { } path && File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    // ---------- FFmpeg setup ----------

    private bool CanInstallFFmpeg() => !IsBusy && !IsInstallingFFmpeg;

    [RelayCommand(CanExecute = nameof(CanInstallFFmpeg))]
    private async Task InstallFFmpegAsync()
    {
        IsInstallingFFmpeg = true;
        InstallFFmpegCommand.NotifyCanExecuteChanged();
        FFmpegInstallProgress = 0;
        string? error = null;

        try
        {
            var progress = new Progress<double?>(p => FFmpegInstallProgress = p);
            await new FFmpegInstaller().InstallAsync(progress, CancellationToken.None);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            IsInstallingFFmpeg = false;
            InstallFFmpegCommand.NotifyCanExecuteChanged();
            RefreshFFmpegState();
        }

        if (error is not null) StatusText = error;
    }

    private void RefreshFFmpegState()
    {
        IsFFmpegAvailable = FFmpegLocator.FindFFmpeg() is not null;
        UpdateStatusText();
    }

    // ---------- Misc ----------

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(QueueSummary));
        ConvertCommand.NotifyCanExecuteChanged();
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (!IsFFmpegAvailable)
        {
            StatusText = "FFmpeg is needed to convert videos.";
            return;
        }

        var failed = Items.Count(i => i.Status == ConversionStatus.Failed);
        StatusText = failed > 0 ? $"{failed} file(s) couldn't be converted – hover a row for details." : QueueSummary;
    }
}
