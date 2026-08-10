using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing;

public class TagLibAudioTagReader : IAudioTagReader
{
    private readonly ILogger<TagLibAudioTagReader> _logger;

    public TagLibAudioTagReader(ILogger<TagLibAudioTagReader> logger)
    {
        _logger = logger;
    }

    public AudioTagData? ReadTags(string filePath, bool includeCoverArt = true)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            var tag = file.Tag;

            if (tag == null)
            {
                return null;
            }

            byte[]? coverData = null;
            string? coverMime = null;

            if (includeCoverArt)
            {
                var frontCover = tag.Pictures?.FirstOrDefault(p => p.Type == TagLib.PictureType.FrontCover)
                              ?? tag.Pictures?.FirstOrDefault();

                if (frontCover?.Data?.Data is { Length: > 0 })
                {
                    coverData = frontCover.Data.Data;
                    coverMime = frontCover.MimeType;
                }
            }

            // ID3v2.3 (and older) uses "/" as a multi-value separator. TagLib splits on it,
            // which turns "AC/DC" into ["AC", "DC"] and later shows up as "AC feat. DC".
            var id3v2 = file.GetTag(TagLib.TagTypes.Id3v2) as TagLib.Id3v2.Tag;
            var repairSlashSplit = id3v2 is not null && id3v2.Version <= 3;

            return new AudioTagData
            {
                Title = NullIfEmpty(tag.Title),
                Album = NullIfEmpty(tag.Album),
                Artists = ReadArtistList(tag.Performers, repairSlashSplit),
                AlbumArtists = ReadArtistList(tag.AlbumArtists, repairSlashSplit),
                TrackNumber = tag.Track > 0 ? (int)tag.Track : null,
                DiscNumber = tag.Disc > 0 ? (int)tag.Disc : null,
                Year = tag.Year > 0 ? (int)tag.Year : null,
                Genres = CleanList(tag.Genres),
                Lyrics = NullIfEmpty(tag.Lyrics),
                Bpm = tag.BeatsPerMinute > 0 ? tag.BeatsPerMinute : null,
                CoverArtData = coverData,
                CoverArtMimeType = coverMime,
                ReplayGainTrackGain = ExtractReplayGain(tag.ReplayGainTrackGain),
                ReplayGainAlbumGain = ExtractReplayGain(tag.ReplayGainAlbumGain),
                MusicBrainzReleaseId = NullIfEmpty(tag.MusicBrainzReleaseId),
                MusicBrainzReleaseGroupId = NullIfEmpty(tag.MusicBrainzReleaseGroupId),
                MusicBrainzArtistId = NullIfEmpty(tag.MusicBrainzArtistId),
                MusicBrainzAlbumArtistId = NullIfEmpty(tag.MusicBrainzReleaseArtistId),
                MusicBrainzRecordingId = NullIfEmpty(tag.MusicBrainzTrackId)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read audio tags from {FilePath}", filePath);
            return null;
        }
    }

    private static IReadOnlyList<string> ReadArtistList(string[]? values, bool repairSlashSplit)
        => repairSlashSplit
            ? MusicArtistNameNormalizer.FromId3v23SplitValues(values)
            : CleanList(values);

    private static IReadOnlyList<string> CleanList(string[]? values)
        => values?.Where(static s => !string.IsNullOrWhiteSpace(s)).Select(static s => s.Trim()).ToList()
           ?? [];

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static double? ExtractReplayGain(double replayGainValue)
        => double.IsNaN(replayGainValue) ? null : replayGainValue;
}
