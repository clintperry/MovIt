namespace MovIt.Services;

/// <summary>The bits of the UI/OS the view model needs: dialogs and launching other apps.</summary>
public interface IPlatformService
{
    /// <summary>Shows a multi-select file picker. Returns an empty list if cancelled.</summary>
    Task<IReadOnlyList<string>> PickVideosAsync(IReadOnlyList<string> extensions);

    /// <summary>Shows a folder picker. Returns null if cancelled.</summary>
    Task<string?> PickFolderAsync(string? startFolder);

    /// <summary>Opens a file with the user's default app.</summary>
    Task PlayAsync(string path);

    /// <summary>Shows a file in Explorer / Finder / the default file manager.</summary>
    Task RevealAsync(string path);
}
