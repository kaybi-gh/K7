namespace K7.Server.Domain.Constants;

public static partial class Constants
{
    public static readonly Dictionary<string, string> ExtensionFormatMapping = new()
    {
        // Audio containers
        { ".aac", "aac" },
        { ".aiff", "aiff" },
        { ".amr", "amr" },
        { ".ape", "ape" },
        { ".flac", "flac" },
        { ".mp3", "mp3" },
        { ".ogg", "ogg" },
        { ".pcm", "pcm" },
        { ".wav", "wav" },
        { ".wma", "wma" },

        // Video containers
        { ".3gp", "3gp" },
        { ".avi", "avi" },
        { ".f4v", "f4v" },
        { ".flv", "flv" },
        { ".m4v", "m4v" },
        { ".mkv", "matroska" },
        { ".mov", "mov" },
        { ".mp4", "mp4" },
        { ".mpg", "mpeg" },
        { ".ts", "mpegts" },
        { ".webm", "webm" },
        { ".wmv", "asf" }
    };

    public static readonly Dictionary<string, string> ContainerMimeTypeMapping = new()
    {
        // Audio containers
        { "aac", "audio/aac" },
        { "aiff", "audio/aiff" },
        { "amr", "audio/amr" },
        { "ape", "audio/ape" },
        { "flac", "audio/flac" },
        { "mp3", "audio/mpeg" },
        { "ogg_audio", "audio/ogg" },
        { "wav", "audio/wav" },
        { "wma", "audio/x-ms-wma" },

        // Video containers
        { "3gp", "video/3gpp" },
        { "avi", "video/x-msvideo" },
        { "asf", "video/x-ms-asf" },
        { "f4v", "video/x-f4v" },
        { "flv", "video/x-flv" },
        { "matroska", "video/x-matroska" },
        { "m4v", "video/x-m4v" },
        { "mov", "video/quicktime" },
        { "mp4", "video/mp4" },
        { "mpeg", "video/mpeg" },
        { "mpegts", "video/mp2t" },
        { "ogg", "video/ogg" },
        { "webm", "video/webm" }
    };
}
