# MovIt

A small cross-platform desktop app (.NET 10 + [Avalonia](https://avaloniaui.net/)) that converts any video file to a QuickTime `.mov` file. Runs on Windows, macOS and Linux.

## Features

- Drag and drop videos or whole folders, or browse for them. Files are converted one after another.
- Four quality presets:
  - **Smart (fastest).** Copies the original streams into a `.mov` container with no quality loss. If the codecs can't go in a `.mov` (VP9, Opus, …), it re-encodes to H.264 instead.
  - **H.264.** Plays on the most devices.
  - **H.265/HEVC.** Smaller files. Tagged `hvc1` so Apple players open them.
  - **Apple ProRes 422 HQ.** For editing.
- An optional **Deinterlace** step (bwdif) for VHS and camcorder captures.
- Progress bar and time remaining for each file, with Cancel, Play and Show in folder buttons.
- Never overwrites anything. If the target name is taken, it saves `name (1).mov` instead.
- Uses the Fluent theme and follows the system light/dark mode.
- Files or folders passed on the command line (`movit clip.mkv folder/`) are added to the queue on launch.

## FFmpeg

MovIt uses FFmpeg to do the converting. It looks for `ffmpeg` (`ffmpeg.exe` on Windows) in these places, in order:

1. Next to the MovIt executable (or in an `ffmpeg/` subfolder)
2. MovIt's private folder: `%LOCALAPPDATA%\MovIt\ffmpeg\bin` (Windows), `~/.local/share/MovIt/ffmpeg/bin` (Linux), `~/Library/Application Support/MovIt/ffmpeg/bin` (macOS)
3. `PATH`
4. Common install folders: `/opt/homebrew/bin`, `/usr/local/bin`, `/opt/local/bin` (macOS); `/usr/bin`, `/usr/local/bin`, `/snap/bin` (Linux)

If it can't find FFmpeg:

- **Windows:** the app shows a **Download FFmpeg** button. This installs a static build from gyan.dev (or BtbN on GitHub if that fails) into the folder in step 2.
- **macOS / Linux:** the app shows how to install it (`brew install ffmpeg`, `sudo apt install ffmpeg`, …) and a **Check again** button.

## Build and run

You need the .NET 10 SDK.

```bash
dotnet run --project src/MovIt
```

Publish for a specific platform (add `-p:PublishSingleFile=true` for a single file):

```bash
dotnet publish src/MovIt -c Release -r win-x64 --self-contained false -o publish/win-x64
dotnet publish src/MovIt -c Release -r osx-arm64 --self-contained -o publish/osx-arm64
dotnet publish src/MovIt -c Release -r linux-x64 --self-contained -o publish/linux-x64
```

Self-contained builds don't need .NET installed. Framework-dependent builds need the .NET 10 runtime.

## Project layout

```
src/MovIt/
  Assets/        MovIt.ico (Windows exe icon) and MovIt.png (window/app icon)
  Models/        ConversionPreset, ConversionStatus
  Services/      FFmpegLocator, FFmpegInstaller, FFmpegConverter, IPlatformService / PlatformService (dialogs, open, reveal)
  ViewModels/    MainViewModel, ConversionItemViewModel (CommunityToolkit.Mvvm)
  Views/         MainWindow (.axaml)
  App.axaml      Fluent theme, brand colours and vector icons
```
