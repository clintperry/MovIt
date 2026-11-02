# MovIt

A small WPF desktop app (.NET 10) that converts any video file to a QuickTime `.mov` file.

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
- Uses the Windows 11 Fluent theme and follows the system light/dark mode.

## FFmpeg

MovIt uses FFmpeg to do the converting. It looks for `ffmpeg.exe` in these places, in order:

1. Next to `MovIt.exe` (or in an `ffmpeg\` subfolder)
2. `%LOCALAPPDATA%\MovIt\ffmpeg\bin`
3. `PATH`

If it can't find FFmpeg, the app shows a **Download FFmpeg** button. This installs a static build from gyan.dev (or BtbN on GitHub if that fails) into the folder in step 2.

## Build and run

```powershell
dotnet run --project src/MovIt
# or publish a single exe:
dotnet publish src/MovIt -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

You need the .NET 10 SDK to build, or the .NET 10 Desktop Runtime to run the published exe.

## Project layout

```
src/MovIt/
  Assets/        MovIt.ico (16–256 px) and MovIt.png
  Models/        ConversionPreset, ConversionStatus
  Services/      FFmpegLocator, FFmpegInstaller, FFmpegConverter
  ViewModels/    MainViewModel, ConversionItemViewModel (CommunityToolkit.Mvvm)
  Views/         MainWindow
  Converters/    InverseBoolToVisibilityConverter
```
