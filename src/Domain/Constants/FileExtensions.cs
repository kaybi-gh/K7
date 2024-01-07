namespace MediaServer.Domain.Constants;

public static class FileExtensions
{
    public static readonly List<string> AudioFileExtensions =
    [
        ".mp3",
        ".wav",
        ".ogg",
        ".flac",
        ".aac",
        ".m4a",
        ".wma"
    ];

    public static readonly List<string> VideoFileExtensions =
    [
        ".mp4",
        ".avi",
        ".mkv",
        ".mov",
        ".wmv",
        ".flv",
        ".webm"
    ];

    public static List<string> GetAll()
    {
        List<string> fileExtensions = [];
        fileExtensions.AddRange(AudioFileExtensions);
        fileExtensions.AddRange(VideoFileExtensions);
        return fileExtensions;
    }
}
