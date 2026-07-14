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

            return new AudioTagData
            {
                Title = NullIfEmpty(tag.Title),
                Album = NullIfEmpty(tag.Album),
                Artists = tag.Performers?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? [],
                AlbumArtists = tag.AlbumArtists?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? [],
                TrackNumber = tag.Track > 0 ? (int)tag.Track : null,
                DiscNumber = tag.Disc > 0 ? (int)tag.Disc : null,
                Year = tag.Year > 0 ? (int)tag.Year : null,
                Genres = tag.Genres?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? [],
                Lyrics = NullIfEmpty(tag.Lyrics),
                Bpm = tag.BeatsPerMinute > 0 ? tag.BeatsPerMinute : null,
                CoverArtData = coverData,
                CoverArtMimeType = coverMime,
                ReplayGainTrackGain = ExtractReplayGain(tag.ReplayGainTrackGain),
                ReplayGainAlbumGain = ExtractReplayGain(tag.ReplayGainAlbumGain)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read audio tags from {FilePath}", filePath);
            return null;
        }
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static double? ExtractReplayGain(double replayGainValue)
        => double.IsNaN(replayGainValue) ? null : replayGainValue;
}
