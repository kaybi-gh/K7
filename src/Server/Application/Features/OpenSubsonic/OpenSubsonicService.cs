using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Devices.Commands.EnsureOpenSubsonicDevice;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;
using K7.Server.Application.Features.Medias.Commands.RateMedia;
using K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;
using K7.Server.Application.Features.Medias.Queries.GetArtistTopTracks;
using K7.Server.Application.Features.Medias.Queries.GetSimilarMusicArtists;
using K7.Server.Application.Features.MusicIntelligence.Queries.GetSimilarTracks;
using K7.Server.Application.Features.Playlists.Commands.AddPlaylistItem;
using K7.Server.Application.Features.Playlists.Commands.CreatePlaylist;
using K7.Server.Application.Features.Playlists.Commands.DeletePlaylist;
using K7.Server.Application.Features.Playlists.Commands.RemovePlaylistItem;
using K7.Server.Application.Features.Playlists.Commands.UpdatePlaylist;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.OpenSubsonic;

public sealed partial class OpenSubsonicService(
    IApplicationDbContext context,
    IUser currentUser,
    IMediaAccessGuard accessGuard,
    MediaAccessFilter mediaAccessFilter,
    ISender sender,
    IActiveStreamTracker activeStreamTracker,
    IOpenSubsonicAudioTranscoder openSubsonicAudioTranscoder,
    ILogger<OpenSubsonicService> logger) : IOpenSubsonicService
{
    private static readonly HashSet<string> UnsupportedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "getVideos", "getVideoInfo", "getCaptions", "hls",
        "getPodcasts", "getPodcastEpisode", "getNewestPodcasts", "createPodcastChannel",
        "deletePodcastChannel", "deletePodcastEpisode", "downloadPodcastEpisode", "refreshPodcasts",
        "getInternetRadioStations", "createInternetRadioStation", "updateInternetRadioStation", "deleteInternetRadioStation",
        "getShares", "createShare", "updateShare", "deleteShare",
        "getChatMessages", "addChatMessage",
        "jukeboxControl",
        "createUser", "updateUser", "deleteUser", "changePassword", "getUsers",
        "createBookmark", "deleteBookmark", "getBookmarks",
        "getPlayQueue", "savePlayQueue", "getPlayQueueByIndex", "savePlayQueueByIndex",
        "getSonicSimilarTracks", "findSonicPath",
        "getTranscodeDecision", "getTranscodeStream"
    };

    public async Task<OpenSubsonicActionResult> ExecuteAsync(
        string action,
        IReadOnlyDictionary<string, string[]> parameters,
        string username,
        bool canWrite,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAction(action);
        if (string.IsNullOrEmpty(normalized))
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorRequiredParam, "Missing action.");

        if (UnsupportedActions.Contains(normalized))
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorNotFound, $"Action '{normalized}' is not supported.");

        try
        {
            return normalized switch
            {
                "ping" => Ping(),
                "getlicense" => GetLicense(),
                "getopensubsonicextensions" => GetOpenSubsonicExtensions(),
                "tokeninfo" => TokenInfo(username),
                "getmusicfolders" => await GetMusicFoldersAsync(cancellationToken),
                "getalbumlist2" => await GetAlbumList2Async(parameters, cancellationToken),
                "getalbumlist" => await GetAlbumList2Async(parameters, cancellationToken, key: "albumList"),
                "getalbum" => await GetAlbumAsync(parameters, cancellationToken),
                "getsong" => await GetSongAsync(parameters, cancellationToken),
                "search3" => await SearchAsync(parameters, cancellationToken, "searchResult3"),
                "search2" => await SearchAsync(parameters, cancellationToken, "searchResult2"),
                "stream" => await StreamOrDownloadAsync(parameters, username, download: false, cancellationToken),
                "download" => await StreamOrDownloadAsync(parameters, username, download: true, cancellationToken),
                "getcoverart" => await GetCoverArtAsync(parameters, cancellationToken),
                "getlyricsbysongid" => await GetLyricsBySongIdAsync(parameters, cancellationToken),
                "getlyrics" => await GetLyricsAsync(parameters, cancellationToken),
                "getplaylists" => await GetPlaylistsAsync(username, cancellationToken),
                "getplaylist" => await GetPlaylistAsync(parameters, username, cancellationToken),
                "createplaylist" => await CreatePlaylistAsync(parameters, canWrite, username, cancellationToken),
                "updateplaylist" => await UpdatePlaylistAsync(parameters, canWrite, cancellationToken),
                "deleteplaylist" => await DeletePlaylistAsync(parameters, canWrite, cancellationToken),
                "getartists" => await GetArtistsAsync(parameters, cancellationToken),
                "getartist" => await GetArtistAsync(parameters, cancellationToken),
                "getartistinfo" => await GetArtistInfoAsync(parameters, responseKey: "artistInfo", cancellationToken),
                "getartistinfo2" => await GetArtistInfoAsync(parameters, responseKey: "artistInfo2", cancellationToken),
                "getalbuminfo" => await GetAlbumInfoAsync(parameters, responseKey: "albumInfo", cancellationToken),
                "getalbuminfo2" => await GetAlbumInfoAsync(parameters, responseKey: "albumInfo2", cancellationToken),
                "getindexes" => await GetIndexesAsync(parameters, cancellationToken),
                "getmusicdirectory" => await GetMusicDirectoryAsync(parameters, cancellationToken),
                "getstarred" => await GetStarredAsync(cancellationToken, "starred"),
                "getstarred2" => await GetStarredAsync(cancellationToken, "starred2"),
                "star" => await StarAsync(parameters, canWrite, cancellationToken),
                "unstar" => await UnstarAsync(parameters, canWrite, cancellationToken),
                "setrating" => await SetRatingAsync(parameters, canWrite, cancellationToken),
                "scrobble" => await ScrobbleAsync(parameters, username, canWrite, cancellationToken),
                "reportplayback" => await ReportPlaybackAsync(parameters, username, canWrite, cancellationToken),
                "getnowplaying" => await GetNowPlayingAsync(cancellationToken),
                "getrandomsongs" => await GetRandomSongsAsync(parameters, cancellationToken),
                "getsongsbygenre" => await GetSongsByGenreAsync(parameters, cancellationToken),
                "getgenres" => await GetGenresAsync(cancellationToken),
                "getsimilarsongs" => await GetSimilarSongsAsync(parameters, responseKey: "similarSongs", cancellationToken),
                "getsimilarsongs2" => await GetSimilarSongsAsync(parameters, responseKey: "similarSongs2", cancellationToken),
                "gettopsongs" => await GetTopSongsAsync(parameters, cancellationToken),
                "getuser" => await GetUserAsync(username, canWrite, cancellationToken),
                "getavatar" => await GetAvatarAsync(parameters, cancellationToken),
                "startscan" => await StartScanAsync(canWrite, cancellationToken),
                "getscanstatus" => await GetScanStatusAsync(cancellationToken),
                _ => OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorGeneric, $"Unknown action '{normalized}'.")
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "OpenSubsonic action {Action} failed", normalized);
            return OpenSubsonicActionResult.Fail(OpenSubsonicConstants.ErrorGeneric, "Internal error.");
        }
    }

    private static string NormalizeAction(string action)
    {
        var value = action.Trim();
        if (value.EndsWith(".view", StringComparison.OrdinalIgnoreCase))
            value = value[..^5];
        return value.ToLowerInvariant();
    }

}
