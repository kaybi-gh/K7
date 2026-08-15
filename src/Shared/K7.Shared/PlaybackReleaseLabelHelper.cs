using System.Globalization;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Shared;

/// <summary>
/// Compact release line for the playback-options file picker:
/// resolution, audio, codec, size, and optional local/federated source.
/// </summary>
public static class PlaybackReleaseLabelHelper
{
    public static string Format(
        IndexedFileDto? file,
        RemoteIndexedFileDto? remote = null,
        string? sourceLabel = null)
    {
        var video = file?.FileMetadata as VideoFileMetadataDto;
        var parts = new List<string>();

        var resolution = video?.VideoResolution ?? remote?.VideoResolution;
        if (resolution is { } res)
            parts.Add(FormatResolution(res));

        var audio = FormatAudioSummary(video?.AudioTracks);
        if (audio is not null)
            parts.Add(audio);

        var codec = video?.VideoTracks.FirstOrDefault()?.Codec;
        if (!string.IsNullOrWhiteSpace(codec))
            parts.Add(codec);

        var size = file?.Size ?? remote?.Size ?? 0;
        if (size > 0)
            parts.Add(FormatSize(size));

        var core = parts.Count > 0
            ? string.Join(" - ", parts)
            : file?.Name ?? remote?.Name ?? "";

        if (string.IsNullOrWhiteSpace(sourceLabel))
            return core;

        return string.IsNullOrWhiteSpace(core) ? sourceLabel : $"{core} ({sourceLabel})";
    }

    public static string FormatResolution(VideoResolutionIdentifier identifier) => identifier switch
    {
        VideoResolutionIdentifier._144p => "144p",
        VideoResolutionIdentifier._240p => "240p",
        VideoResolutionIdentifier._360p => "360p",
        VideoResolutionIdentifier._480p => "480p",
        VideoResolutionIdentifier._720p => "720p",
        VideoResolutionIdentifier._1080p => "1080p",
        VideoResolutionIdentifier._1440p => "1440p",
        VideoResolutionIdentifier._2160p => "4K",
        VideoResolutionIdentifier._4320p => "8K",
        _ => identifier.ToString().TrimStart('_')
    };

    public static string FormatSize(long bytes)
    {
        var culture = CultureInfo.CurrentCulture;
        const double gb = 1024d * 1024 * 1024;
        const double mb = 1024d * 1024;

        if (bytes >= gb)
            return string.Format(culture, "{0:0.#} GB", bytes / gb);

        if (bytes >= mb)
            return string.Format(culture, "{0:0.#} MB", bytes / mb);

        return string.Format(culture, "{0:0.#} KB", bytes / 1024d);
    }

    public static string? FormatAudioSummary(IReadOnlyList<AudioFileTrackDto>? tracks)
    {
        if (tracks is not { Count: > 0 })
            return null;

        var labels = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var track in tracks)
        {
            var distinctive = AudioTrackDisplayHelper.GetDistinctiveName(track.Name, track.Language);
            var label = distinctive
                ?? (string.IsNullOrWhiteSpace(track.Language) || LanguageNormalizer.IsUndetermined(track.Language)
                    ? null
                    : track.Language.ToUpperInvariant());

            if (label is null || !seen.Add(label))
                continue;

            labels.Add(label);
        }

        return labels.Count == 0 ? null : string.Join(", ", labels);
    }
}
