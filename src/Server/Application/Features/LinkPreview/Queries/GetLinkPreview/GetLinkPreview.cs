using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.LinkPreview.Queries.GetLinkPreview;

public sealed record LinkPreview(
    string Title,
    string? Description,
    Guid? PictureId);

public record GetLinkPreviewQuery(string Path) : IRequest<LinkPreview?>;

public class GetLinkPreviewQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLinkPreviewQuery, LinkPreview?>
{
    private const int DescriptionMaxLength = 300;

    private static readonly MetadataPictureType[] PosterFirst =
        [MetadataPictureType.Poster, MetadataPictureType.Cover, MetadataPictureType.Still];
    private static readonly MetadataPictureType[] CoverFirst =
        [MetadataPictureType.Cover, MetadataPictureType.Poster];
    private static readonly MetadataPictureType[] StillFirst =
        [MetadataPictureType.Still, MetadataPictureType.Poster, MetadataPictureType.Cover];

    public async Task<LinkPreview?> Handle(GetLinkPreviewQuery request, CancellationToken cancellationToken)
    {
        if (!LinkPreviewRoute.TryParse(request.Path, out var target))
            return null;

        return target.Kind switch
        {
            LinkPreviewKind.Season => await GetSeasonAsync(target, cancellationToken),
            LinkPreviewKind.Episode => await GetEpisodeAsync(target, cancellationToken),
            _ => await GetMediaAsync(target, cancellationToken)
        };
    }

    private async Task<LinkPreview?> GetMediaAsync(LinkPreviewTarget target, CancellationToken cancellationToken)
    {
        var expected = target.Kind switch
        {
            LinkPreviewKind.Movie => MediaType.Movie,
            LinkPreviewKind.Serie => MediaType.Serie,
            LinkPreviewKind.Album => MediaType.MusicAlbum,
            LinkPreviewKind.Artist => MediaType.MusicArtist,
            _ => (MediaType?)null
        };
        if (expected is not { } expectedType)
            return null;

        var media = await context.Medias
            .AsNoTracking()
            .Include(m => m.Pictures)
            .FirstOrDefaultAsync(m => m.Id == target.Id && m.Type == expectedType, cancellationToken);

        if (media is null || string.IsNullOrWhiteSpace(media.Title))
            return null;

        return new LinkPreview(media.Title, Truncate(OverviewOf(media)), PictureId(media.Pictures, target.Kind));
    }

    private async Task<LinkPreview?> GetSeasonAsync(LinkPreviewTarget target, CancellationToken cancellationToken)
    {
        if (target.SeasonNumber is not { } seasonNumber)
            return null;

        var season = await context.Medias
            .OfType<SerieSeason>()
            .AsNoTracking()
            .Include(s => s.Pictures)
            .Include(s => s.Serie)
                .ThenInclude(s => s.Pictures)
            .FirstOrDefaultAsync(
                s => s.SerieId == target.Id && s.SeasonNumber == seasonNumber,
                cancellationToken);

        if (season is null)
            return null;

        var serieTitle = season.Serie?.Title;
        var title = !string.IsNullOrWhiteSpace(season.Title)
            ? string.IsNullOrWhiteSpace(serieTitle) ? season.Title : $"{serieTitle}: {season.Title}"
            : string.IsNullOrWhiteSpace(serieTitle) ? $"S{seasonNumber}" : $"{serieTitle} S{seasonNumber}";

        var pictureId = PictureId(season.Pictures, LinkPreviewKind.Season)
            ?? PictureId(season.Serie?.Pictures, LinkPreviewKind.Serie);

        return new LinkPreview(title, Truncate(season.Overview ?? season.Serie?.Overview), pictureId);
    }

    private async Task<LinkPreview?> GetEpisodeAsync(LinkPreviewTarget target, CancellationToken cancellationToken)
    {
        if (target.SeasonNumber is not { } seasonNumber || target.EpisodeNumber is not { } episodeNumber)
            return null;

        var episode = await context.Medias
            .OfType<SerieEpisode>()
            .AsNoTracking()
            .Include(e => e.Pictures)
            .Include(e => e.Season)
                .ThenInclude(s => s.Pictures)
            .Include(e => e.Serie)
                .ThenInclude(s => s.Pictures)
            .FirstOrDefaultAsync(
                e => e.SerieId == target.Id
                    && e.Season.SeasonNumber == seasonNumber
                    && e.EpisodeNumber == episodeNumber,
                cancellationToken);

        if (episode is null)
            return null;

        var serieTitle = episode.Serie?.Title;
        var title = !string.IsNullOrWhiteSpace(episode.Title)
            ? string.IsNullOrWhiteSpace(serieTitle) ? episode.Title : $"{serieTitle} - {episode.Title}"
            : string.IsNullOrWhiteSpace(serieTitle)
                ? $"S{seasonNumber}E{episodeNumber}"
                : $"{serieTitle} S{seasonNumber}E{episodeNumber}";

        var pictureId = PictureId(episode.Pictures, LinkPreviewKind.Episode)
            ?? PictureId(episode.Season?.Pictures, LinkPreviewKind.Season)
            ?? PictureId(episode.Serie?.Pictures, LinkPreviewKind.Serie);

        return new LinkPreview(title, Truncate(episode.Overview), pictureId);
    }

    private static string? OverviewOf(BaseMedia media) => media switch
    {
        Movie movie => movie.Overview,
        Serie serie => serie.Overview,
        MusicAlbum album => album.Overview,
        MusicArtist artist => artist.Biography,
        _ => null
    };

    private static Guid? PictureId(IEnumerable<MetadataPicture>? pictures, LinkPreviewKind kind)
    {
        if (pictures is null)
            return null;

        var preferred = kind switch
        {
            LinkPreviewKind.Album or LinkPreviewKind.Artist => CoverFirst,
            LinkPreviewKind.Episode => StillFirst,
            _ => PosterFirst
        };

        foreach (var type in preferred)
        {
            var match = pictures.FirstOrDefault(p => p.Type == type && p.LocalPath is not null);
            if (match is not null)
                return match.Id;
        }

        return pictures.FirstOrDefault(p => p.LocalPath is not null)?.Id;
    }

    private static string? Truncate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();
        if (trimmed.Length <= DescriptionMaxLength)
            return trimmed;

        return trimmed[..DescriptionMaxLength].TrimEnd();
    }
}
