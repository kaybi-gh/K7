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
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.OpenSubsonic;

public sealed partial class OpenSubsonicService
{
    private static OpenSubsonicAlbum MapAlbum(MusicAlbum album, Guid userId, bool includeSongs, int? songCount = null)
    {
        var userRating = album.Ratings.OfType<UserRating>().FirstOrDefault(r => r.UserId == userId);
        var genre = album.MetadataTags
            .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
            .Select(mt => mt.MetadataTag.DisplayName)
            .FirstOrDefault();

        var duration = includeSongs
            ? album.Tracks.Sum(GetDurationSeconds)
            : (int?)null;

        return new OpenSubsonicAlbum
        {
            Id = album.Id.ToString("D"),
            Name = album.Title ?? string.Empty,
            Title = album.Title,
            Album = album.Title,
            Artist = album.Artist?.Title,
            ArtistId = album.ArtistId?.ToString("D"),
            CoverArt = album.Id.ToString("D"),
            SongCount = songCount ?? album.Tracks.Count,
            Duration = duration,
            PlayCount = album.UserMediaStates.FirstOrDefault(s => s.UserId == userId)?.PlayCount,
            Created = FormatDate(album.Created),
            Year = album.ReleaseDate?.Year,
            Genre = genre,
            Starred = IsStarred(userRating) ? FormatDate(userRating!.LastModified == default ? album.Created : userRating.LastModified) : null,
            UserRating = ToOsRating(userRating?.Value),
            Parent = album.ArtistId?.ToString("D"),
            IsDir = true,
            Song = includeSongs
                ? album.Tracks
                    .OrderBy(t => t.DiscNumber)
                    .ThenBy(t => t.TrackNumber)
                    .Select(t => MapSong(t, userId))
                    .ToList()
                : null
        };
    }

    private static OpenSubsonicSong MapSong(MusicTrack track, Guid userId)
    {
        var file = track.IndexedFiles.OrderBy(f => f.Created).FirstOrDefault();
        var userRating = track.Ratings.OfType<UserRating>().FirstOrDefault(r => r.UserId == userId);
        var genre = track.MetadataTags
            .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
            .Select(mt => mt.MetadataTag.DisplayName)
            .FirstOrDefault()
            ?? track.Album?.MetadataTags
                .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                .Select(mt => mt.MetadataTag.DisplayName)
                .FirstOrDefault();

        var artist = track.Artist ?? track.Album?.Artist;
        var artistId = track.ArtistId ?? track.Album?.ArtistId;

        return new OpenSubsonicSong
        {
            Id = track.Id.ToString("D"),
            Parent = track.AlbumId.ToString("D"),
            Title = track.Title ?? string.Empty,
            Album = track.Album?.Title,
            AlbumId = track.AlbumId.ToString("D"),
            Artist = artist?.Title,
            ArtistId = artistId?.ToString("D"),
            Track = track.TrackNumber,
            Year = track.Album?.ReleaseDate?.Year ?? track.ReleaseDate?.Year,
            Genre = genre,
            CoverArt = track.AlbumId.ToString("D"),
            Size = file?.Size,
            ContentType = null,
            Suffix = file?.Extension?.TrimStart('.'),
            Duration = GetDurationSeconds(track),
            BitRate = null,
            Path = file?.Name,
            IsDir = false,
            Type = "music",
            DiscNumber = track.DiscNumber,
            Created = FormatDate(track.Created),
            Starred = IsStarred(userRating) ? FormatDate(userRating!.LastModified == default ? track.Created : userRating.LastModified) : null,
            UserRating = ToOsRating(userRating?.Value),
            PlayCount = track.UserMediaStates.FirstOrDefault(s => s.UserId == userId)?.PlayCount
        };
    }

    private static OpenSubsonicArtist MapArtist(MusicArtist artist, Guid userId)
    {
        var userRating = artist.Ratings.OfType<UserRating>().FirstOrDefault(r => r.UserId == userId);
        return new OpenSubsonicArtist
        {
            Id = artist.Id.ToString("D"),
            Name = artist.Title ?? string.Empty,
            AlbumCount = artist.Albums?.Count,
            CoverArt = artist.Id.ToString("D"),
            Starred = IsStarred(userRating) ? FormatDate(userRating!.LastModified == default ? artist.Created : userRating.LastModified) : null,
            UserRating = ToOsRating(userRating?.Value)
        };
    }

    private static OpenSubsonicPlaylist MapPlaylist(Playlist playlist, string username, bool includeEntries) =>
        new()
        {
            Id = playlist.Id.ToString("D"),
            Name = playlist.Title,
            Comment = playlist.Description,
            Owner = username,
            Public = playlist.VisibilityScope != VisibilityScope.Nobody,
            SongCount = playlist.Items.Count,
            Created = FormatDate(playlist.Created),
            Changed = FormatDate(playlist.LastModified == default ? playlist.Created : playlist.LastModified),
            CoverArt = playlist.CoverPicture is not null ? playlist.Id.ToString("D") : null,
            Entry = includeEntries ? [] : null
        };

    private static List<OpenSubsonicStructuredLyrics> BuildStructuredLyrics(MusicTrack track)
    {
        var list = new List<OpenSubsonicStructuredLyrics>();
        if (!string.IsNullOrWhiteSpace(track.LyricsLrc))
        {
            list.Add(new OpenSubsonicStructuredLyrics
            {
                DisplayArtist = track.Artist?.Title,
                DisplayTitle = track.Title,
                Synced = true,
                Line = ParseLrc(track.LyricsLrc)
            });
        }

        if (!string.IsNullOrWhiteSpace(track.Lyrics))
        {
            list.Add(new OpenSubsonicStructuredLyrics
            {
                DisplayArtist = track.Artist?.Title,
                DisplayTitle = track.Title,
                Synced = false,
                Line = track.Lyrics.Replace("\r\n", "\n").Split('\n')
                    .Select(line => new OpenSubsonicLyricLine { Value = line })
                    .ToList()
            });
        }

        return list;
    }

    private static List<OpenSubsonicLyricLine> ParseLrc(string lrc)
    {
        var lines = new List<OpenSubsonicLyricLine>();
        foreach (var raw in lrc.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length < 10 || line[0] != '[')
                continue;

            var close = line.IndexOf(']');
            if (close <= 1)
                continue;

            var stamp = line[1..close];
            var text = line[(close + 1)..];
            if (!TimeSpan.TryParseExact(stamp, @"mm\:ss\.ff", CultureInfo.InvariantCulture, out var ts)
                && !TimeSpan.TryParseExact(stamp, @"mm\:ss\.fff", CultureInfo.InvariantCulture, out ts))
                continue;

            lines.Add(new OpenSubsonicLyricLine
            {
                Value = text,
                Start = (long)ts.TotalMilliseconds
            });
        }

        return lines;
    }

    private static int GetDurationSeconds(MusicTrack track)
    {
        var audio = track.IndexedFiles
            .Select(f => f.FileMetadata)
            .OfType<AudioFileMetadata>()
            .FirstOrDefault();
        return audio is null ? 0 : (int)Math.Round(audio.Duration.TotalSeconds);
    }

    private static bool IsStarred(UserRating? rating) =>
        rating is not null && rating.Value > OpenSubsonicConstants.StarredThreshold;

    private static int? ToOsRating(double? k7Value) =>
        k7Value is null ? null : (int)Math.Round(k7Value.Value / OpenSubsonicConstants.RatingScaleFactor);

    private static string FormatDate(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static string GetIndexName(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "#";

        var trimmed = title.Trim();
        foreach (var article in new[] { "The ", "El ", "La ", "Los ", "Las ", "Le ", "Les " })
        {
            if (trimmed.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[article.Length..].TrimStart();
                break;
            }
        }

        if (string.IsNullOrEmpty(trimmed))
            return "#";

        var ch = char.ToUpperInvariant(trimmed[0]);
        return char.IsLetter(ch) ? ch.ToString() : "#";
    }

    private static string GuessImageContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

}
