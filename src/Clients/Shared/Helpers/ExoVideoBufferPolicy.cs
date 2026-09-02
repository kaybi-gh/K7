using K7.Server.Domain.Common;

namespace K7.Clients.Shared.Helpers;

public static class VideoAudioPassthroughCodecs
{
    public static bool IsPassthrough(string? codec) => AudioPassthroughCodecs.IsPassthrough(codec);
}

public enum ExoVideoBufferSize
{
    Auto,
    Default,
    Large,
    ExtraLarge
}

public static class ExoVideoBufferPolicy
{
    public const string Auto = "auto";
    public const string Default = "default";
    public const string Large = "large";
    public const string ExtraLarge = "extralarge";

    public static ExoVideoBufferSize Parse(string? stored) => stored?.Trim().ToLowerInvariant() switch
    {
        Default => ExoVideoBufferSize.Default,
        Large => ExoVideoBufferSize.Large,
        ExtraLarge => ExoVideoBufferSize.ExtraLarge,
        _ => ExoVideoBufferSize.Auto
    };

    public static string Persist(ExoVideoBufferSize size) => size switch
    {
        ExoVideoBufferSize.Default => Default,
        ExoVideoBufferSize.Large => Large,
        ExoVideoBufferSize.ExtraLarge => ExtraLarge,
        _ => Auto
    };

    public static ExoVideoBufferSize Resolve(ExoVideoBufferSize stored, bool isTelevision)
    {
        _ = isTelevision;
        return stored == ExoVideoBufferSize.Auto
            ? ExoVideoBufferSize.Default
            : stored;
    }
}
