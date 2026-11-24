using CommunityToolkit.Mvvm.ComponentModel;
using MovIt.Models;

namespace MovIt.ViewModels;

public sealed partial class ConversionItemViewModel(string sourcePath) : ObservableObject
{
    public string SourcePath { get; } = sourcePath;

    public string FileName => Path.GetFileName(SourcePath);

    public string SourceFolder => Path.GetDirectoryName(SourcePath) ?? string.Empty;

    public string SizeText { get; } = FormatSize(sourcePath);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive), nameof(IsFinished), nameof(IsQueued), nameof(IsDone), nameof(IsFailed), nameof(IsCancelled), nameof(CanOpen))]
    public partial ConversionStatus Status { get; set; } = ConversionStatus.Queued;

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = "Waiting";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpen))]
    public partial string? OutputPath { get; set; }

    public bool IsActive => Status == ConversionStatus.Converting;

    public bool IsFinished => Status is ConversionStatus.Done or ConversionStatus.Failed or ConversionStatus.Cancelled;

    public bool CanOpen => Status == ConversionStatus.Done && OutputPath is not null;

    public bool IsQueued => Status == ConversionStatus.Queued;

    public bool IsDone => Status == ConversionStatus.Done;

    public bool IsFailed => Status == ConversionStatus.Failed;

    public bool IsCancelled => Status == ConversionStatus.Cancelled;

    public void Reset()
    {
        Status = ConversionStatus.Queued;
        Progress = 0;
        IsIndeterminate = false;
        Message = "Waiting";
        OutputPath = null;
    }

    private static string FormatSize(string path)
    {
        try
        {
            double bytes = new FileInfo(path).Length;
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            var unit = 0;
            while (bytes >= 1024 && unit < units.Length - 1)
            {
                bytes /= 1024;
                unit++;
            }
            return $"{bytes:0.#} {units[unit]}";
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }
}
