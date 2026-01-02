using K7.Server.Domain.Enums;

namespace K7.Server.Application.Helpers;

public static class MimeTypeHelper
{
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
