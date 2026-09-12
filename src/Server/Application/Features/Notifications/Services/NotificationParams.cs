using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Features.Notifications.Services;

public static class NotificationParams
{
    public static NotificationParameterInfo Text(
        string name,
        NotificationParameterGroup group,
        string sample) =>
        Create(name, "String", group, sample, RuleFieldValueType.Text);

    public static NotificationParameterInfo Number(
        string name,
        NotificationParameterGroup group,
        string sample,
        string valueType = "Int32") =>
        Create(name, valueType, group, sample, RuleFieldValueType.Number);

    public static NotificationParameterInfo Bool(
        string name,
        NotificationParameterGroup group,
        string sample = "true") =>
        Create(name, "Boolean", group, sample, RuleFieldValueType.Boolean, BooleanOptions);

    public static NotificationParameterInfo Select(
        string name,
        NotificationParameterGroup group,
        string sample,
        IReadOnlyList<RuleFieldOptionDto> options) =>
        Create(name, "String", group, sample, RuleFieldValueType.Select, options);

    public static NotificationParameterInfo Search(
        string name,
        NotificationParameterGroup group,
        string sample) =>
        Create(name, "String", group, sample, RuleFieldValueType.Search);

    public static NotificationParameterInfo Language(
        string name,
        NotificationParameterGroup group,
        string sample) =>
        Create(name, "String", group, sample, RuleFieldValueType.Language);

    public static NotificationParameterInfo Date(
        string name,
        NotificationParameterGroup group,
        string sample) =>
        Create(name, "DateTime", group, sample, RuleFieldValueType.Date);

    private static NotificationParameterInfo Create(
        string name,
        string valueType,
        NotificationParameterGroup group,
        string sample,
        RuleFieldValueType filterValueType,
        IReadOnlyList<RuleFieldOptionDto>? options = null)
    {
        var key = "Param" + name.Replace(".", "", StringComparison.Ordinal);
        return new NotificationParameterInfo(name, key, valueType, group, sample, filterValueType, options);
    }

    public static readonly IReadOnlyList<RuleFieldOptionDto> MediaTypeOptions =
        Enum.GetValues<MediaType>()
            .Select(v => new RuleFieldOptionDto { Value = v.ToString(), Label = v.ToString() })
            .ToList();

    public static readonly IReadOnlyList<RuleFieldOptionDto> PlaybackStateOptions =
        Enum.GetValues<Domain.Enums.PlaybackState>()
            .Where(v => v is not Domain.Enums.PlaybackState.Unknown)
            .Select(v => new RuleFieldOptionDto
            {
                Value = v == Domain.Enums.PlaybackState.Idle ? "Stopped" : v.ToString(),
                Label = v == Domain.Enums.PlaybackState.Idle ? "Stopped" : v.ToString()
            })
            .ToList();

    public static readonly IReadOnlyList<RuleFieldOptionDto> DeviceTypeOptions =
        Enum.GetValues<DeviceType>()
            .Select(v => new RuleFieldOptionDto { Value = v.ToString(), Label = v.ToString() })
            .ToList();

    public static readonly IReadOnlyList<RuleFieldOptionDto> ClientTypeOptions =
        Enum.GetValues<ClientType>()
            .Select(v => new RuleFieldOptionDto { Value = v.ToString(), Label = v.ToString() })
            .ToList();

    public static readonly IReadOnlyList<RuleFieldOptionDto> UserCreationOriginOptions =
        Enum.GetValues<UserCreationOrigin>()
            .Select(v => new RuleFieldOptionDto { Value = v.ToString(), Label = v.ToString() })
            .ToList();

    public static readonly IReadOnlyList<RuleFieldOptionDto> ApiKeyScopeOptions =
        Enum.GetValues<ApiKeyScope>()
            .Select(v => new RuleFieldOptionDto { Value = v.ToString(), Label = v.ToString() })
            .ToList();

    private static readonly IReadOnlyList<RuleFieldOptionDto> BooleanOptions =
    [
        new() { Value = "true", Label = "true" },
        new() { Value = "false", Label = "false" }
    ];

    public static readonly NotificationParameterInfo EventType =
        Text("EventType", NotificationParameterGroup.Global, "MediaAddedEvent");

    public static readonly NotificationParameterInfo ServerName =
        Text("Server.Name", NotificationParameterGroup.Server, "K7");

    public static readonly NotificationParameterInfo ServerUrl =
        Text("Server.Url", NotificationParameterGroup.Server, "https://k7.example.com");

    public static readonly NotificationParameterInfo ServerVersion =
        Text("Server.Version", NotificationParameterGroup.Server, "1.0.0");

    public static readonly NotificationParameterInfo CurrentYear =
        Number("Current.Year", NotificationParameterGroup.Global, "2026");

    public static readonly NotificationParameterInfo CurrentMonth =
        Number("Current.Month", NotificationParameterGroup.Global, "5");

    public static readonly NotificationParameterInfo CurrentDay =
        Number("Current.Day", NotificationParameterGroup.Global, "24");

    public static readonly NotificationParameterInfo CurrentHour =
        Number("Current.Hour", NotificationParameterGroup.Global, "14");

    public static readonly NotificationParameterInfo CurrentMinute =
        Number("Current.Minute", NotificationParameterGroup.Global, "30");

    public static readonly NotificationParameterInfo CurrentWeekday =
        Text("Current.Weekday", NotificationParameterGroup.Global, "Saturday");

    public static readonly NotificationParameterInfo CurrentDatestamp =
        Date("Current.Datestamp", NotificationParameterGroup.Global, "2026-05-24");

    public static readonly NotificationParameterInfo CurrentTimestamp =
        Date("Current.Timestamp", NotificationParameterGroup.Global, "2026-05-24T14:30:00Z");

    public static readonly NotificationParameterInfo CurrentUnixTime =
        Number("Current.UnixTime", NotificationParameterGroup.Global, "1748092200", "Int64");

    public static readonly NotificationParameterInfo MediaTitle =
        Search("Media.Title", NotificationParameterGroup.Media, "Interstellar");

    public static readonly NotificationParameterInfo MediaOriginalTitle =
        Text("Media.OriginalTitle", NotificationParameterGroup.Media, "Interstellar");

    public static readonly NotificationParameterInfo MediaType =
        Select("Media.Type", NotificationParameterGroup.Media, "Movie", MediaTypeOptions);

    public static readonly NotificationParameterInfo MediaReleaseDate =
        Date("Media.ReleaseDate", NotificationParameterGroup.Media, "2014-11-05");

    public static readonly NotificationParameterInfo MediaYear =
        Number("Media.Year", NotificationParameterGroup.Media, "2014");

    public static readonly NotificationParameterInfo MediaGenres =
        Search("Media.Genres", NotificationParameterGroup.Media, "Science Fiction, Drama");

    public static readonly NotificationParameterInfo MediaGenresCount =
        Number("Media.Genres.Count", NotificationParameterGroup.Media, "3");

    public static readonly NotificationParameterInfo MediaIndexedFilesCount =
        Number("Media.IndexedFiles.Count", NotificationParameterGroup.Media, "1");

    public static readonly NotificationParameterInfo PictureUrl =
        Text("PictureUrl", NotificationParameterGroup.Media, "https://k7.example.com/api/metadata-pictures/a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static readonly NotificationParameterInfo BackdropUrl =
        Text("BackdropUrl", NotificationParameterGroup.Media, "https://k7.example.com/api/metadata-pictures/f9e8d7c6-b5a4-3210-fedc-ba9876543210");

    public static readonly NotificationParameterInfo MediaUrl =
        Text("Media.Url", NotificationParameterGroup.Media, "https://k7.example.com/movies/a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static readonly NotificationParameterInfo ExternalTmdb =
        Text("External.Tmdb", NotificationParameterGroup.Media, "157336");

    public static readonly NotificationParameterInfo ExternalImdb =
        Text("External.Imdb", NotificationParameterGroup.Media, "tt0816692");

    public static readonly NotificationParameterInfo ExternalTvdb =
        Text("External.Tvdb", NotificationParameterGroup.Media, "121361");

    public static readonly NotificationParameterInfo ExternalMusicBrainz =
        Text("External.MusicBrainz", NotificationParameterGroup.Music, "b10bbbfc-cf9e-42e0-be17-e2c3e1d2600d");

    public static readonly NotificationParameterInfo ShowName =
        Search("Show.Name", NotificationParameterGroup.Show, "Breaking Bad");

    public static readonly NotificationParameterInfo SeasonNumber =
        Number("Season.Number", NotificationParameterGroup.Show, "1");

    public static readonly NotificationParameterInfo EpisodeNumber =
        Number("Episode.Number", NotificationParameterGroup.Show, "1");

    public static readonly NotificationParameterInfo EpisodeName =
        Text("Episode.Name", NotificationParameterGroup.Show, "Pilot");

    public static readonly NotificationParameterInfo ArtistName =
        Search("Artist.Name", NotificationParameterGroup.Music, "Radiohead");

    public static readonly NotificationParameterInfo AlbumName =
        Search("Album.Name", NotificationParameterGroup.Music, "OK Computer");

    public static readonly NotificationParameterInfo TrackName =
        Text("Track.Name", NotificationParameterGroup.Music, "Paranoid Android");

    public static readonly NotificationParameterInfo TrackNumber =
        Number("Track.Number", NotificationParameterGroup.Music, "2");

    public static readonly NotificationParameterInfo UserName =
        Search("User.Name", NotificationParameterGroup.User, "alice");

    public static readonly NotificationParameterInfo UserId =
        Text("User.Id", NotificationParameterGroup.User, "a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static readonly NotificationParameterInfo UserEmail =
        Text("User.Email", NotificationParameterGroup.User, "alice@example.com");

    public static readonly NotificationParameterInfo UserRole =
        Text("User.Role", NotificationParameterGroup.User, "User");

    public static readonly NotificationParameterInfo UserOrigin =
        Select("User.Origin", NotificationParameterGroup.User, "Registration", UserCreationOriginOptions);

    public static readonly NotificationParameterInfo RatingValue =
        Number("Rating.Value", NotificationParameterGroup.Rating, "8");

    public static readonly NotificationParameterInfo RatingIsNew =
        Bool("Rating.IsNew", NotificationParameterGroup.Rating);

    public static readonly NotificationParameterInfo ReviewText =
        Text("Review.Text", NotificationParameterGroup.Review, "Great movie.");

    public static readonly NotificationParameterInfo ReviewEmoji =
        Text("Review.Emoji", NotificationParameterGroup.Review, "star");

    public static readonly NotificationParameterInfo ReviewIsNew =
        Bool("Review.IsNew", NotificationParameterGroup.Review);

    public static readonly NotificationParameterInfo ApiKeyName =
        Text("ApiKey.Name", NotificationParameterGroup.Security, "CI deploy");

    public static readonly NotificationParameterInfo ApiKeyScope =
        Select("ApiKey.Scope", NotificationParameterGroup.Security, "Read", ApiKeyScopeOptions);

    public static readonly NotificationParameterInfo ApiKeyKeyPrefix =
        Text("ApiKey.KeyPrefix", NotificationParameterGroup.Security, "k7_ab12");

    public static readonly NotificationParameterInfo ClientAppPasswordName =
        Text("ClientAppPassword.Name", NotificationParameterGroup.Security, "Subsonic phone");

    public static readonly NotificationParameterInfo HiddenIsHidden =
        Bool("Hidden.IsHidden", NotificationParameterGroup.Hidden);

    public static readonly NotificationParameterInfo HiddenIsSelfExcluded =
        Bool("Hidden.IsSelfExcluded", NotificationParameterGroup.Hidden);

    public static readonly NotificationParameterInfo HiddenIsAdminExcluded =
        Bool("Hidden.IsAdminExcluded", NotificationParameterGroup.Hidden);

    public static readonly NotificationParameterInfo SessionState =
        Select("Session.State", NotificationParameterGroup.Session, "Playing", PlaybackStateOptions);

    public static readonly NotificationParameterInfo SessionPreviousState =
        Select("Session.PreviousState", NotificationParameterGroup.Session, "Paused", PlaybackStateOptions);

    public static readonly NotificationParameterInfo SessionPosition =
        Number("Session.Position", NotificationParameterGroup.Session, "120", "Float");

    public static readonly NotificationParameterInfo SessionDuration =
        Number("Session.Duration", NotificationParameterGroup.Session, "5400", "Float");

    public static readonly NotificationParameterInfo SessionProgressPercent =
        Number("Session.ProgressPercent", NotificationParameterGroup.Session, "42", "Float");

    public static readonly NotificationParameterInfo SessionWatchedDuration =
        Number("Session.WatchedDurationSeconds", NotificationParameterGroup.Session, "2100", "Float");

    public static readonly NotificationParameterInfo SessionDurationSeconds =
        Number("Session.DurationSeconds", NotificationParameterGroup.Session, "5400", "Float");

    public static readonly NotificationParameterInfo SessionUserId =
        Text("Session.UserId", NotificationParameterGroup.Session, "a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static readonly NotificationParameterInfo SessionMediaId =
        Text("Session.MediaId", NotificationParameterGroup.Session, "b2c3d4e5-f6a7-8901-bcde-f12345678901");

    public static readonly NotificationParameterInfo SessionDeviceId =
        Text("Session.DeviceId", NotificationParameterGroup.Session, "c3d4e5f6-a7b8-9012-cdef-123456789012");

    public static readonly NotificationParameterInfo PlaybackState =
        Select("State", NotificationParameterGroup.Session, "Playing", PlaybackStateOptions);

    public static readonly NotificationParameterInfo PlaybackPreviousState =
        Select("PreviousState", NotificationParameterGroup.Session, "Paused", PlaybackStateOptions);

    public static readonly NotificationParameterInfo PlaybackUserName =
        Search("UserName", NotificationParameterGroup.User, "alice");

    public static readonly NotificationParameterInfo PlaybackUserId =
        Text("UserId", NotificationParameterGroup.User, "a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static readonly NotificationParameterInfo PlaybackMediaTitle =
        Search("MediaTitle", NotificationParameterGroup.Media, "Interstellar");

    public static readonly NotificationParameterInfo PlaybackMediaType =
        Select("MediaType", NotificationParameterGroup.Media, "Movie", MediaTypeOptions);

    public static readonly NotificationParameterInfo PlaybackLibraryTitle =
        Search("LibraryTitle", NotificationParameterGroup.Library, "Films");

    public static readonly NotificationParameterInfo PlaybackDeviceName =
        Text("DeviceName", NotificationParameterGroup.Device, "Galaxy S24");

    public static readonly NotificationParameterInfo PlaybackDeviceType =
        Select("DeviceType", NotificationParameterGroup.Device, "Phone", DeviceTypeOptions);

    public static readonly NotificationParameterInfo PlaybackPosition =
        Number("Position", NotificationParameterGroup.Session, "120", "Float");

    public static readonly NotificationParameterInfo PlaybackDuration =
        Number("Duration", NotificationParameterGroup.Session, "5400", "Float");

    public static readonly NotificationParameterInfo LibraryTitle =
        Search("Library.Title", NotificationParameterGroup.Library, "Films");

    public static readonly NotificationParameterInfo LibraryMediaType =
        Select("Library.MediaType", NotificationParameterGroup.Library, "Movie", MediaTypeOptions);

    public static readonly NotificationParameterInfo LibraryRootPath =
        Text("Library.RootPath", NotificationParameterGroup.Library, "/media/movies");

    public static readonly NotificationParameterInfo LibraryMetadataProvider =
        Text("Library.MetadataProviderName", NotificationParameterGroup.Library, "TMDB");

    public static readonly NotificationParameterInfo LibraryMetadataLanguage =
        Language("Library.MetadataLanguage", NotificationParameterGroup.Library, "fr");

    public static readonly NotificationParameterInfo LibraryId =
        Text("Library.Id", NotificationParameterGroup.Library, "a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static readonly NotificationParameterInfo AddedCount =
        Number("AddedCount", NotificationParameterGroup.Library, "12");

    public static readonly NotificationParameterInfo SkippedCount =
        Number("SkippedCount", NotificationParameterGroup.Library, "3");

    public static readonly NotificationParameterInfo InaccessibleCount =
        Number("InaccessibleCount", NotificationParameterGroup.Library, "1");

    public static readonly NotificationParameterInfo DeviceName =
        Text("Device.DeviceName", NotificationParameterGroup.Device, "Galaxy S24");

    public static readonly NotificationParameterInfo DeviceDeviceType =
        Select("Device.DeviceType", NotificationParameterGroup.Device, "Phone", DeviceTypeOptions);

    public static readonly NotificationParameterInfo DeviceClientType =
        Select("Device.ClientType", NotificationParameterGroup.Device, "Native", ClientTypeOptions);

    public static readonly NotificationParameterInfo DeviceOs =
        Text("Device.OperatingSystem", NotificationParameterGroup.Device, "Android");

    public static readonly NotificationParameterInfo DeviceOsVersion =
        Text("Device.OperatingSystemVersion", NotificationParameterGroup.Device, "Android 15");

    public static readonly NotificationParameterInfo DeviceScreenWidth =
        Number("Device.DisplayScreenWidth", NotificationParameterGroup.Device, "1080", "Float");

    public static readonly NotificationParameterInfo DeviceScreenHeight =
        Number("Device.DisplayScreenHeight", NotificationParameterGroup.Device, "2340", "Float");

    public static readonly NotificationParameterInfo DeviceResWidth =
        Number("Device.DisplayResolutionWidth", NotificationParameterGroup.Device, "1080", "Float");

    public static readonly NotificationParameterInfo DeviceResHeight =
        Number("Device.DisplayResolutionHeight", NotificationParameterGroup.Device, "2340", "Float");

    public static readonly NotificationParameterInfo DeviceUniqueId =
        Text("Device.DeviceUniqueId", NotificationParameterGroup.Device, "abc123");

    public static readonly NotificationParameterInfo DeviceLastSeen =
        Date("Device.LastSeen", NotificationParameterGroup.Device, "2026-05-24T12:00:00Z");

    public static readonly NotificationParameterInfo IndexedFileName =
        Text("IndexedFile.Name", NotificationParameterGroup.File, "interstellar.mkv");

    public static readonly NotificationParameterInfo IndexedFileExtension =
        Text("IndexedFile.Extension", NotificationParameterGroup.File, ".mkv");

    public static readonly NotificationParameterInfo IndexedFilePath =
        Text("IndexedFile.Path", NotificationParameterGroup.File, "/media/movies/interstellar.mkv");

    public static readonly NotificationParameterInfo IndexedFileParent =
        Text("IndexedFile.ParentDirectory", NotificationParameterGroup.File, "/media/movies");

    public static readonly NotificationParameterInfo IndexedFileSize =
        Number("IndexedFile.Size", NotificationParameterGroup.File, "4200000000", "Int64");

    public static readonly NotificationParameterInfo IndexedFileLibraryId =
        Text("IndexedFile.LibraryId", NotificationParameterGroup.File, "a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static readonly NotificationParameterInfo FileType =
        Text("FileType", NotificationParameterGroup.File, "Video");

    public static readonly NotificationParameterInfo PlaylistTitle =
        Text("Playlist.Title", NotificationParameterGroup.Playlist, "Road Trip");

    public static readonly NotificationParameterInfo PlaylistDescription =
        Text("Playlist.Description", NotificationParameterGroup.Playlist, "Songs for the road");

    public static readonly NotificationParameterInfo PlaylistMediaType =
        Select("Playlist.MediaType", NotificationParameterGroup.Playlist, "MusicTrack", MediaTypeOptions);

    public static readonly NotificationParameterInfo PlaylistItemsCount =
        Number("Playlist.Items.Count", NotificationParameterGroup.Playlist, "42");

    public static readonly NotificationParameterInfo PlaylistItemMediaId =
        Text("Item.MediaId", NotificationParameterGroup.Playlist, "a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static readonly NotificationParameterInfo PlaylistItemOrder =
        Number("Item.Order", NotificationParameterGroup.Playlist, "3");

    public static readonly NotificationParameterInfo CollectionTitle =
        Text("Collection.Title", NotificationParameterGroup.Playlist, "Marvel");

    public static readonly NotificationParameterInfo CollectionDescription =
        Text("Collection.Description", NotificationParameterGroup.Playlist, "MCU movies");

    public static readonly NotificationParameterInfo CollectionIsPublic =
        Bool("Collection.IsPublic", NotificationParameterGroup.Playlist);

    public static readonly NotificationParameterInfo CollectionMediaType =
        Select("Collection.MediaType", NotificationParameterGroup.Playlist, "Movie", MediaTypeOptions);

    public static readonly NotificationParameterInfo CollectionItemsCount =
        Number("Collection.Items.Count", NotificationParameterGroup.Playlist, "33");

    public static readonly NotificationParameterInfo DynamicPlaylistTitle =
        Text("DynamicPlaylist.Title", NotificationParameterGroup.Playlist, "Recently Added");

    public static readonly NotificationParameterInfo DynamicPlaylistDescription =
        Text("DynamicPlaylist.Description", NotificationParameterGroup.Playlist, "Last 30 days");

    public static readonly NotificationParameterInfo DynamicPlaylistMediaType =
        Select("DynamicPlaylist.MediaType", NotificationParameterGroup.Playlist, "Movie", MediaTypeOptions);

    public static readonly NotificationParameterInfo DynamicPlaylistLimit =
        Number("DynamicPlaylist.Limit", NotificationParameterGroup.Playlist, "50");

    public static readonly NotificationParameterInfo DynamicPlaylistOrderBy =
        Text("DynamicPlaylist.OrderBy", NotificationParameterGroup.Playlist, "DateAdded");

    public static readonly NotificationParameterInfo DynamicPlaylistOrderDirection =
        Text("DynamicPlaylist.OrderDirection", NotificationParameterGroup.Playlist, "Descending");

    public static readonly NotificationParameterInfo DownloadStatus =
        Text("Download.Status", NotificationParameterGroup.Download, "Ready");

    public static readonly NotificationParameterInfo DownloadIsDirect =
        Bool("Download.IsDirectStream", NotificationParameterGroup.Download);

    public static readonly NotificationParameterInfo DownloadContentType =
        Text("Download.ContentType", NotificationParameterGroup.Download, "video/mp4");

    public static readonly NotificationParameterInfo DownloadFileSize =
        Number("Download.FileSize", NotificationParameterGroup.Download, "4200000000", "Int64");

    public static readonly NotificationParameterInfo DownloadIndexedFileId =
        Text("Download.IndexedFileId", NotificationParameterGroup.Download, "a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static readonly NotificationParameterInfo DownloadDeviceId =
        Text("Download.DeviceId", NotificationParameterGroup.Download, "e5f6g7h8-a9b0-1234-cdef-123456789012");

    public static readonly NotificationParameterInfo DownloadUserId =
        Text("Download.UserId", NotificationParameterGroup.Download, "i9j0k1l2-m3n4-5678-cdef-123456789012");

    public static readonly NotificationParameterInfo PeerName =
        Text("Peer.Name", NotificationParameterGroup.Federation, "Home");

    public static readonly NotificationParameterInfo PeerBaseUrl =
        Text("Peer.BaseUrl", NotificationParameterGroup.Federation, "https://peer.example.com");

    public static readonly NotificationParameterInfo PeerId =
        Text("Peer.Id", NotificationParameterGroup.Federation, "a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static readonly NotificationParameterInfo PeerSucceeded =
        Bool("Succeeded", NotificationParameterGroup.Federation);

    public static readonly NotificationParameterInfo PeerPreviousSucceeded =
        Bool("PreviousSucceeded", NotificationParameterGroup.Federation, "false");

    public static readonly NotificationParameterInfo TranscodeIndexedFileId =
        Text("IndexedFileId", NotificationParameterGroup.Health, "a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static readonly NotificationParameterInfo TranscodeMediaTitle =
        Text("MediaTitle", NotificationParameterGroup.Health, "Interstellar");

    public static readonly NotificationParameterInfo TranscodeError =
        Text("ErrorMessage", NotificationParameterGroup.Health, "Encoder failed");

    public static readonly NotificationParameterInfo MusicIntelligenceReason =
        Text("Reason", NotificationParameterGroup.Health, "Connection refused");

    public static readonly NotificationParameterInfo ClientErrorMessage =
        Text("Message", NotificationParameterGroup.Health, "NullReferenceException: Object reference not set");

    public static readonly NotificationParameterInfo ClientErrorSource =
        Text("Source", NotificationParameterGroup.Health, "K7.Clients.Shared.UI");

    public static readonly NotificationParameterInfo ClientErrorStackTrace =
        Text("StackTrace", NotificationParameterGroup.Health, "at K7.Clients...");

    public static readonly NotificationParameterInfo ClientErrorDeviceId =
        Text("DeviceId", NotificationParameterGroup.Health, "c63d6c9e-f80c-4180-b0f8-b1dc3f69056f");

    public static readonly NotificationParameterInfo ClientErrorDeviceName =
        Text("DeviceName", NotificationParameterGroup.Health, "Living Room TV");

    public static readonly NotificationParameterInfo ClientErrorUserName =
        Text("UserName", NotificationParameterGroup.Health, "anonymous");

    public static readonly IReadOnlyList<NotificationParameterInfo> Globals =
    [
        EventType, ServerName, ServerUrl, ServerVersion,
        CurrentYear, CurrentMonth, CurrentDay, CurrentHour, CurrentMinute,
        CurrentWeekday, CurrentDatestamp, CurrentTimestamp, CurrentUnixTime
    ];

    public static readonly IReadOnlyList<NotificationParameterInfo> MediaCore =
    [
        MediaTitle, MediaOriginalTitle, MediaType, MediaReleaseDate, MediaYear,
        MediaGenres, MediaGenresCount, PictureUrl, BackdropUrl, MediaUrl,
        ExternalTmdb, ExternalImdb, ExternalTvdb
    ];

    public static readonly IReadOnlyList<NotificationParameterInfo> PlaybackCompleted =
    [
        SessionUserId, SessionMediaId, SessionDurationSeconds, SessionWatchedDuration,
        SessionState, SessionDeviceId, MediaTitle, MediaType, MediaYear, MediaGenres,
        ShowName, SeasonNumber, EpisodeNumber, EpisodeName,
        ArtistName, AlbumName, TrackName, TrackNumber,
        UserName, UserId, PictureUrl, MediaUrl,
        ExternalTmdb, ExternalImdb, ExternalTvdb, ExternalMusicBrainz,
        SessionProgressPercent
    ];

    public static readonly IReadOnlyList<NotificationParameterInfo> PlaybackStateChanged =
    [
        PlaybackState, PlaybackPreviousState, PlaybackUserName, PlaybackUserId,
        PlaybackMediaTitle, PlaybackMediaType, PlaybackLibraryTitle,
        PlaybackDeviceName, PlaybackDeviceType, PlaybackPosition, PlaybackDuration,
        UserName, UserId, MediaTitle, MediaType, MediaYear, MediaGenres,
        ShowName, SeasonNumber, EpisodeNumber, EpisodeName,
        ArtistName, AlbumName, TrackName, TrackNumber,
        SessionProgressPercent, PictureUrl, MediaUrl,
        ExternalTmdb, ExternalImdb, ExternalTvdb, ExternalMusicBrainz
    ];
}
