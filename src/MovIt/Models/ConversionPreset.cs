namespace MovIt.Models;

public enum PresetKind
{
    /// <summary>Copy streams into a .mov container when possible; fall back to H.264 if not.</summary>
    Smart,
    /// <summary>H.264 video + AAC audio. Plays almost everywhere.</summary>
    H264,
    /// <summary>H.265/HEVC video + AAC audio, tagged hvc1 for Apple players.</summary>
    Hevc,
    /// <summary>Apple ProRes 422 HQ + PCM audio. Large files, ideal for editing.</summary>
    ProRes,
}

public sealed record ConversionPreset(PresetKind Kind, string Name, string Description)
{
    public static IReadOnlyList<ConversionPreset> All { get; } =
    [
        new(PresetKind.Smart, "Smart (fastest)",
            "Re-wraps the original video and audio into .mov with no quality loss. Falls back to H.264 if the codecs aren't .mov compatible."),
        new(PresetKind.H264, "H.264 – most compatible",
            "Re-encodes to H.264 + AAC. Plays on practically every device and editor."),
        new(PresetKind.Hevc, "H.265 / HEVC – smaller files",
            "Re-encodes to HEVC + AAC. About half the size of H.264 at similar quality; slower to encode."),
        new(PresetKind.ProRes, "Apple ProRes 422 HQ – editing",
            "Edit-friendly intermediate format with uncompressed audio. Very large files."),
    ];

    public bool ReEncodes => Kind != PresetKind.Smart;

    public override string ToString() => Name;
}
