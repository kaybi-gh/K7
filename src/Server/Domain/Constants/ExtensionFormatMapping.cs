namespace K7.Server.Domain.Constants;

public static partial class Constants
{
    public static readonly Dictionary<string, string> ExtensionFormatMapping = new()
    {
        { ".mp3", "mp3" },
        { ".aac", "aac" },
        { ".mp4", "mp4" },
        { ".mov", "mov" },
        { ".mkv", "matroska" },
        { ".webm", "webm" },
        { ".avi", "avi" },
        { ".flv", "flv" },
        { ".m4v", "m4v" },
        { ".wmv", "asf" },
        { ".ogg", "ogg" },
        { ".wav", "wav" },
        { ".mpg", "mpeg" },
        { ".ts", "mpegts" },
        { ".3gp", "3gp" },
        { ".ape", "ape" },
        { ".amr", "amr" },
        { ".aiff", "aiff" },
        { ".wma", "wma" },
        { ".f4v", "f4v" },
        { ".flac", "flac" },
        { ".pcm", "pcm" }
    };
}
