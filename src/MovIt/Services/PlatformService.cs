using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MovIt.Services;

/// <summary>Avalonia/OS implementation of <see cref="IPlatformService"/>.</summary>
public sealed class PlatformService(TopLevel owner) : IPlatformService
{
    public async Task<IReadOnlyList<string>> PickVideosAsync(IReadOnlyList<string> extensions)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose videos to convert",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Video files") { Patterns = [.. extensions.Select(e => "*" + e)] },
                FilePickerFileTypes.All,
            ],
        });

        return [.. files.Select(f => f.TryGetLocalPath()).OfType<string>()];
    }

    public async Task<string?> PickFolderAsync(string? startFolder)
    {
        var options = new FolderPickerOpenOptions { Title = "Save converted videos to…", AllowMultiple = false };
        if (startFolder is not null)
            options.SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(startFolder);

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(options);
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task PlayAsync(string path)
    {
        if (owner.Launcher is { } launcher)
            await launcher.LaunchFileInfoAsync(new FileInfo(path));
    }

    public async Task RevealAsync(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { $"/select,{path}" } });
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo("open") { ArgumentList = { "-R", path } });
        }
        else if (owner.Launcher is { } launcher && Path.GetDirectoryName(path) is { } folder)
        {
            // No portable "select this file" on Linux; open the containing folder instead.
            await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder));
        }
    }
}
