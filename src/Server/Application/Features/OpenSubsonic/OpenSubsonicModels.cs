using System.Text.Json.Serialization;

namespace K7.Server.Application.Features.OpenSubsonic;

public sealed class OpenSubsonicError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("helpUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HelpUrl { get; init; }
}

public sealed class OpenSubsonicBinaryPayload
{
    public Guid? IndexedFileId { get; init; }
    public string? FilePath { get; init; }
    public string? ContentType { get; init; }
    public string? FileDownloadName { get; init; }
    public bool EnableRangeProcessing { get; init; } = true;
    public Func<Stream>? OpenStream { get; init; }
    /// <summary>Device-scoped active-stream session used to track HTTP body lifetime.</summary>
    public Guid? TransferSessionId { get; init; }
    public Guid? TransferMediaId { get; init; }
}

public sealed class OpenSubsonicActionResult
{
    public OpenSubsonicError? Error { get; private init; }
    public IReadOnlyDictionary<string, object?>? Data { get; private init; }
    public OpenSubsonicBinaryPayload? Binary { get; private init; }

    public bool IsBinary => Binary is not null;
    public bool IsFailed => Error is not null;

    public static OpenSubsonicActionResult Ok(IReadOnlyDictionary<string, object?>? data = null) =>
        new() { Data = data };

    public static OpenSubsonicActionResult OkEmpty() => new() { Data = null };

    public static OpenSubsonicActionResult Fail(int code, string message, string? helpUrl = null) =>
        new()
        {
            Error = new OpenSubsonicError
            {
                Code = code,
                Message = message,
                HelpUrl = helpUrl
            }
        };

    public static OpenSubsonicActionResult File(
        string filePath,
        string? contentType = null,
        string? fileDownloadName = null,
        bool enableRangeProcessing = true) =>
        new()
        {
            Binary = new OpenSubsonicBinaryPayload
            {
                FilePath = filePath,
                ContentType = contentType,
                FileDownloadName = fileDownloadName,
                EnableRangeProcessing = enableRangeProcessing
            }
        };

    public static OpenSubsonicActionResult IndexedFile(
        Guid indexedFileId,
        string? fileDownloadName = null,
        bool enableRangeProcessing = true,
        Guid? transferSessionId = null,
        Guid? transferMediaId = null) =>
        new()
        {
            Binary = new OpenSubsonicBinaryPayload
            {
                IndexedFileId = indexedFileId,
                FileDownloadName = fileDownloadName,
                EnableRangeProcessing = enableRangeProcessing,
                TransferSessionId = transferSessionId,
                TransferMediaId = transferMediaId
            }
        };

    public static OpenSubsonicActionResult ProgressiveStream(
        Func<Stream> openStream,
        string contentType,
        string? fileDownloadName = null,
        Guid? transferSessionId = null,
        Guid? transferMediaId = null) =>
        new()
        {
            Binary = new OpenSubsonicBinaryPayload
            {
                OpenStream = openStream,
                ContentType = contentType,
                FileDownloadName = fileDownloadName,
                EnableRangeProcessing = false,
                TransferSessionId = transferSessionId,
                TransferMediaId = transferMediaId
            }
        };
}

public sealed class OpenSubsonicMusicFolder
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public sealed class OpenSubsonicArtist
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("albumCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AlbumCount { get; init; }

    [JsonPropertyName("coverArt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CoverArt { get; init; }

    [JsonPropertyName("artistImageUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ArtistImageUrl { get; init; }

    [JsonPropertyName("starred")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Starred { get; init; }

    [JsonPropertyName("userRating")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? UserRating { get; init; }

    [JsonPropertyName("averageRating")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AverageRating { get; init; }

    [JsonPropertyName("album")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpenSubsonicAlbum>? Album { get; init; }
}

public sealed record OpenSubsonicAlbum
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName("album")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Album { get; init; }

    [JsonPropertyName("artist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Artist { get; init; }

    [JsonPropertyName("artistId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ArtistId { get; init; }

    [JsonPropertyName("coverArt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CoverArt { get; init; }

    [JsonPropertyName("songCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SongCount { get; init; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Duration { get; init; }

    [JsonPropertyName("playCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? PlayCount { get; init; }

    [JsonPropertyName("created")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Created { get; init; }

    [JsonPropertyName("year")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Year { get; init; }

    [JsonPropertyName("genre")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Genre { get; init; }

    [JsonPropertyName("starred")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Starred { get; init; }

    [JsonPropertyName("userRating")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? UserRating { get; init; }

    [JsonPropertyName("averageRating")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AverageRating { get; init; }

    [JsonPropertyName("parent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Parent { get; init; }

    [JsonPropertyName("isDir")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsDir { get; init; }

    [JsonPropertyName("song")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpenSubsonicSong>? Song { get; init; }
}

public sealed class OpenSubsonicSong
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("parent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Parent { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("album")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Album { get; init; }

    [JsonPropertyName("albumId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AlbumId { get; init; }

    [JsonPropertyName("artist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Artist { get; init; }

    [JsonPropertyName("artistId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ArtistId { get; init; }

    [JsonPropertyName("track")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Track { get; init; }

    [JsonPropertyName("year")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Year { get; init; }

    [JsonPropertyName("genre")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Genre { get; init; }

    [JsonPropertyName("coverArt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CoverArt { get; init; }

    [JsonPropertyName("size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Size { get; init; }

    [JsonPropertyName("contentType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentType { get; init; }

    [JsonPropertyName("suffix")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Suffix { get; init; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Duration { get; init; }

    [JsonPropertyName("bitRate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BitRate { get; init; }

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; init; }

    [JsonPropertyName("isDir")]
    public bool IsDir { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "music";

    [JsonPropertyName("discNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DiscNumber { get; init; }

    [JsonPropertyName("created")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Created { get; init; }

    [JsonPropertyName("starred")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Starred { get; init; }

    [JsonPropertyName("userRating")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? UserRating { get; init; }

    [JsonPropertyName("averageRating")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AverageRating { get; init; }

    [JsonPropertyName("playCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? PlayCount { get; init; }
}

public sealed class OpenSubsonicPlaylist
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }

    [JsonPropertyName("owner")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Owner { get; init; }

    [JsonPropertyName("public")]
    public bool Public { get; init; }

    [JsonPropertyName("songCount")]
    public int SongCount { get; init; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Duration { get; init; }

    [JsonPropertyName("created")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Created { get; init; }

    [JsonPropertyName("changed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Changed { get; init; }

    [JsonPropertyName("coverArt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CoverArt { get; init; }

    [JsonPropertyName("entry")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpenSubsonicSong>? Entry { get; init; }
}

public sealed class OpenSubsonicGenre
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("songCount")]
    public int SongCount { get; init; }

    [JsonPropertyName("albumCount")]
    public int AlbumCount { get; init; }
}

public sealed class OpenSubsonicIndex
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("artist")]
    public List<OpenSubsonicArtist> Artist { get; init; } = [];
}

public sealed class OpenSubsonicDirectory
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("parent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Parent { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("starred")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Starred { get; init; }

    [JsonPropertyName("userRating")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? UserRating { get; init; }

    [JsonPropertyName("averageRating")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AverageRating { get; init; }

    [JsonPropertyName("child")]
    public List<object> Child { get; init; } = [];
}

public sealed class OpenSubsonicLyricsList
{
    [JsonPropertyName("structuredLyrics")]
    public List<OpenSubsonicStructuredLyrics> StructuredLyrics { get; init; } = [];
}

public sealed class OpenSubsonicStructuredLyrics
{
    [JsonPropertyName("displayArtist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayArtist { get; init; }

    [JsonPropertyName("displayTitle")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayTitle { get; init; }

    [JsonPropertyName("lang")]
    public string Lang { get; init; } = "und";

    [JsonPropertyName("synced")]
    public bool Synced { get; init; }

    [JsonPropertyName("line")]
    public List<OpenSubsonicLyricLine> Line { get; init; } = [];
}

public sealed class OpenSubsonicLyricLine
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("start")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Start { get; init; }
}

public sealed class OpenSubsonicExtension
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("versions")]
    public List<int> Versions { get; init; } = [];
}

public sealed class OpenSubsonicUser
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; init; }

    [JsonPropertyName("scrobblingEnabled")]
    public bool ScrobblingEnabled { get; init; } = true;

    [JsonPropertyName("adminRole")]
    public bool AdminRole { get; init; }

    [JsonPropertyName("settingsRole")]
    public bool SettingsRole { get; init; }

    [JsonPropertyName("downloadRole")]
    public bool DownloadRole { get; init; } = true;

    [JsonPropertyName("uploadRole")]
    public bool UploadRole { get; init; }

    [JsonPropertyName("playlistRole")]
    public bool PlaylistRole { get; init; } = true;

    [JsonPropertyName("coverArtRole")]
    public bool CoverArtRole { get; init; }

    [JsonPropertyName("commentRole")]
    public bool CommentRole { get; init; }

    [JsonPropertyName("podcastRole")]
    public bool PodcastRole { get; init; }

    [JsonPropertyName("streamRole")]
    public bool StreamRole { get; init; } = true;

    [JsonPropertyName("jukeboxRole")]
    public bool JukeboxRole { get; init; }

    [JsonPropertyName("shareRole")]
    public bool ShareRole { get; init; }

    [JsonPropertyName("videoConversionRole")]
    public bool VideoConversionRole { get; init; }

    [JsonPropertyName("folder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? Folder { get; init; }
}

public sealed class OpenSubsonicNowPlayingEntry
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("minutesAgo")]
    public int MinutesAgo { get; init; }

    [JsonPropertyName("playerId")]
    public int PlayerId { get; init; }

    [JsonPropertyName("playerName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlayerName { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("album")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Album { get; init; }

    [JsonPropertyName("artist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Artist { get; init; }

    [JsonPropertyName("isDir")]
    public bool IsDir { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "music";

    [JsonPropertyName("state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; init; }

    [JsonPropertyName("positionMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? PositionMs { get; init; }

    [JsonPropertyName("playbackRate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PlaybackRate { get; init; }
}

public sealed class OpenSubsonicArtistInfo
{
    [JsonPropertyName("biography")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Biography { get; init; }

    [JsonPropertyName("musicBrainzId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MusicBrainzId { get; init; }

    [JsonPropertyName("lastFmUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastFmUrl { get; init; }

    [JsonPropertyName("smallImageUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SmallImageUrl { get; init; }

    [JsonPropertyName("mediumImageUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediumImageUrl { get; init; }

    [JsonPropertyName("largeImageUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LargeImageUrl { get; init; }

    [JsonPropertyName("similarArtist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpenSubsonicArtist>? SimilarArtist { get; init; }
}

public sealed class OpenSubsonicAlbumInfo
{
    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; init; }

    [JsonPropertyName("musicBrainzId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MusicBrainzId { get; init; }

    [JsonPropertyName("lastFmUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastFmUrl { get; init; }

    [JsonPropertyName("smallImageUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SmallImageUrl { get; init; }

    [JsonPropertyName("mediumImageUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediumImageUrl { get; init; }

    [JsonPropertyName("largeImageUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LargeImageUrl { get; init; }
}

public sealed class OpenSubsonicScanStatus
{
    [JsonPropertyName("scanning")]
    public bool Scanning { get; init; }

    [JsonPropertyName("count")]
    public long Count { get; init; }
}
