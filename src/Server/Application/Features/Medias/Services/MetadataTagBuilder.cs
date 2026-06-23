using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Medias.Services;

public static class MetadataTagBuilder
{
    public static IReadOnlyList<MetadataTagDesired> FromMovieMetadata(ExternalMovieMetadata metadata, Movie movie) =>
        Build(movie, [
            (MetadataTagKind.Genre, "Genres", metadata.Genres, null),
            (MetadataTagKind.Studio, "Studios", metadata.Studios, null),
            (MetadataTagKind.ContentRating, "ContentRating", null, metadata.ContentRating)
        ]);

    public static IReadOnlyList<MetadataTagDesired> FromSerieMetadata(ExternalSerieMetadata metadata, Serie serie) =>
        Build(serie, [
            (MetadataTagKind.Genre, "Genres", metadata.Genres, null),
            (MetadataTagKind.Studio, "Studios", metadata.Studios, null),
            (MetadataTagKind.ContentRating, "ContentRating", null, metadata.ContentRating),
            (MetadataTagKind.Network, "Network", null, metadata.Network)
        ]);

    public static IReadOnlyList<MetadataTagDesired> FromMusicAlbumMetadata(ExternalMusicAlbumMetadata metadata, MusicAlbum album) =>
        Build(album, [(MetadataTagKind.Genre, "Genres", metadata.Genres)]);

    public static IReadOnlyList<MetadataTagDesired> FromGenres(BaseMedia media, IEnumerable<string>? genres) =>
        Build(media, [(MetadataTagKind.Genre, "Genres", genres)]);

    public static IReadOnlyList<MetadataTagDesired> FromManualUpdate(
        BaseMedia media,
        IList<string>? genres,
        string? contentRating,
        string? network)
    {
        var specs = new List<(MetadataTagKind Kind, string LockField, IEnumerable<string>? Values, string? Single)>
        {
            (MetadataTagKind.Genre, "Genres", genres, null)
        };

        if (media is Movie or Serie)
            specs.Add((MetadataTagKind.ContentRating, "ContentRating", null, contentRating));

        if (media is Serie)
            specs.Add((MetadataTagKind.Network, "Network", null, network));

        return Build(media, specs);
    }

    public static IReadOnlyList<MetadataTagDesired> FromPeerMetadata(
        BaseMedia media,
        IReadOnlyList<string> genres,
        IReadOnlyList<string> studios,
        string? contentRating,
        string? network)
    {
        var specs = new List<(MetadataTagKind Kind, string LockField, IEnumerable<string>? Values, string? Single)>
        {
            (MetadataTagKind.Genre, "Genres", genres, null)
        };

        if (media is Movie or Serie)
        {
            specs.Add((MetadataTagKind.Studio, "Studios", studios, null));
            specs.Add((MetadataTagKind.ContentRating, "ContentRating", null, contentRating));
        }

        if (media is Serie)
            specs.Add((MetadataTagKind.Network, "Network", null, network));

        return Build(media, specs);
    }

    private static IReadOnlyList<MetadataTagDesired> Build(
        BaseMedia media,
        IEnumerable<(MetadataTagKind Kind, string LockField, IEnumerable<string>? Values)> specs) =>
        Build(media, specs.Select(s => (s.Kind, s.LockField, s.Values, (string?)null)));

    private static IReadOnlyList<MetadataTagDesired> Build(
        BaseMedia media,
        IEnumerable<(MetadataTagKind Kind, string LockField, IEnumerable<string>? Values, string? Single)> specs)
    {
        var results = new List<MetadataTagDesired>();

        foreach (var (kind, lockField, values, single) in specs)
        {
            if (media.IsFieldLocked(lockField))
                continue;

            if (values is not null)
            {
                if (kind == MetadataTagKind.Genre)
                    AddGenreValues(results, values);
                else
                    AddMultiValues(results, kind, values);
            }

            if (!string.IsNullOrWhiteSpace(single))
                AddSingleValue(results, kind, single);
        }

        return results
            .GroupBy(t => (t.Kind, t.NormalizedKey))
            .Select(g => g.First())
            .ToList();
    }

    private static void AddGenreValues(List<MetadataTagDesired> results, IEnumerable<string> genres)
    {
        foreach (var genre in genres)
        {
            foreach (var part in MetadataTagNormalizer.SplitMultiValue(genre, splitGenreDelimiters: true))
                AddSingleValue(results, MetadataTagKind.Genre, part);
        }
    }

    private static void AddMultiValues(List<MetadataTagDesired> results, MetadataTagKind kind, IEnumerable<string> values)
    {
        foreach (var value in values)
            AddSingleValue(results, kind, value);
    }

    private static void AddSingleValue(List<MetadataTagDesired> results, MetadataTagKind kind, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var displayName = value.Trim();
        var normalizedKey = MetadataTagNormalizer.NormalizeKey(displayName);
        if (normalizedKey.Length == 0)
            return;

        results.Add(new MetadataTagDesired(kind, normalizedKey, displayName));
    }
}

public sealed record MetadataTagDesired(MetadataTagKind Kind, string NormalizedKey, string DisplayName);
