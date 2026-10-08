namespace K7.Server.Domain.Interfaces;

/// <summary>
/// ffmpeg -i source (local file path).
/// </summary>
public sealed record FfmpegMediaInput(string PathOrUrl)
{
    public static FfmpegMediaInput FromFile(string path) => new(path);

    public string DisplayName => Path.GetFileName(PathOrUrl);
}
