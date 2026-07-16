using K7.Server.Domain.Enums;

namespace K7.Server.Application.Helpers;

public static class MimeTypeHelper
{
    public const string HlsPlaylist = "application/vnd.apple.mpegurl";
    public const string Binary = "application/octet-stream";

    public static string GetImageContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".webp" => "image/webp",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".svg" => "image/svg+xml",
        ".gif" => "image/gif",
        _ => Binary
    };

    public static string GetStreamContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".m3u8" => HlsPlaylist,
        ".m4s" => "video/iso.segment",
        ".mp4" => "video/mp4",
        _ => Binary
    };

    public static string? GetImageExtension(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        return contentType.Split(';', 2)[0].Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/svg+xml" => ".svg",
            _ => null
        };
    }

    public static string GetMimeType(MediaFormatType type, string container)
    {
        return type switch
        {
            MediaFormatType.Audio => container.ToLower() switch
            {
                "mp3" => "audio/mpeg",
                "mp4" => "audio/mp4",
                "webm" => "audio/webm",
                "ogg" => "audio/ogg",
                "flac" => "audio/flac",
                "wav" => "audio/wav",
                "aac" => "audio/aac",
                "wma" => "audio/wma",
                "ape" => "audio/ape",
                _ => "application/octet-stream"
            },
            MediaFormatType.Video => container.ToLower() switch
            {
                "mp4" => "video/mp4",
                "webm" => "video/webm",
                "mkv" => "video/x-matroska",
                "avi" => "video/x-msvideo",
                "flv" => "video/x-flv",
                "mov" => "video/quicktime",
                "m4v" => "video/mp4",
                _ => "application/octet-stream"
            },
            _ => "application/octet-stream"
        };
    }
}
