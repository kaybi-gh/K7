using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Features.Medias.Commands.UpdateMediaMetadata;

[Authorize(Roles = Roles.Administrator)]
public record UpdateMediaMetadataCommand : IRequest
{
    public required Guid Id { get; init; }
    public required IList<string> LockedFields { get; init; }

    // BaseMedia fields
    public string? Title { get; init; }
    public string? SortTitle { get; init; }
    public string? OriginalTitle { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public IList<string>? Genres { get; init; }

    // Movie fields
    public string? Tagline { get; init; }
    public string? Overview { get; init; }
    public string? OriginalLanguage { get; init; }
    public string? ContentRating { get; init; }
    public long? Budget { get; init; }
    public long? Revenue { get; init; }

    // Serie fields
    public string? Status { get; init; }
    public string? Network { get; init; }

    // SerieEpisode fields
    public DateOnly? AirDate { get; init; }
    public int? Runtime { get; init; }

    // MusicArtist fields
    public string? Biography { get; init; }
    public string? Country { get; init; }

    // MusicTrack fields
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public string? Lyrics { get; init; }
    public string? LyricsLrc { get; init; }
    public IList<ExternalIdEditDto>? ExternalIds { get; init; }
}

public class UpdateMediaMetadataCommandHandler(
    IApplicationDbContext context,
    IMediaMetadataTagSyncService metadataTagSyncService)
    : IRequestHandler<UpdateMediaMetadataCommand>
{
    public async Task Handle(UpdateMediaMetadataCommand request, CancellationToken cancellationToken)
    {
        var media = await context.Medias
            .Include(m => m.ExternalIds)
            .Include(m => m.MetadataTags)
                .ThenInclude(mt => mt.MetadataTag)
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, media);

        media.LockedFields = request.LockedFields;

        // BaseMedia fields
        if (request.Title is not null)
            media.Title = request.Title;
        if (request.SortTitle is not null)
            media.SortTitle = request.SortTitle;
        else if (request.Title is not null && !media.IsFieldLocked(nameof(BaseMedia.SortTitle)))
            media.SortTitle = MediaSortTitleHelper.Compute(request.Title);
        if (request.OriginalTitle is not null)
            media.OriginalTitle = request.OriginalTitle;
        if (request.ReleaseDate is not null)
            media.ReleaseDate = request.ReleaseDate;
        if (request.Overview is not null)
            ApplyOverview(media, request.Overview);

        // Type-specific fields
        switch (media)
        {
            case Movie movie:
                if (request.Tagline is not null) movie.Tagline = request.Tagline;
                if (request.OriginalLanguage is not null) movie.OriginalLanguage = request.OriginalLanguage;
                if (request.Budget is not null) movie.Budget = request.Budget;
                if (request.Revenue is not null) movie.Revenue = request.Revenue;
                break;

            case Serie serie:
                if (request.OriginalLanguage is not null) serie.OriginalLanguage = request.OriginalLanguage;
                if (request.Status is not null) serie.Status = request.Status;
                break;

            case SerieSeason:
                break;

            case SerieEpisode episode:
                if (request.AirDate is not null) episode.AirDate = request.AirDate;
                if (request.Runtime is not null) episode.Runtime = request.Runtime;
                break;

            case MusicAlbum:
                break;

            case MusicArtist artist:
                if (request.Biography is not null) artist.Biography = request.Biography;
                if (request.Country is not null) artist.Country = request.Country;
                break;

            case MusicTrack track:
                if (request.TrackNumber is not null) track.TrackNumber = request.TrackNumber;
                if (request.DiscNumber is not null) track.DiscNumber = request.DiscNumber;
                if (request.Lyrics is not null) track.Lyrics = request.Lyrics;
                if (request.LyricsLrc is not null) track.LyricsLrc = request.LyricsLrc;
                break;
        }

        if (request.ExternalIds is not null)
        {
            media.ExternalIds.Clear();
            foreach (var dto in request.ExternalIds)
                media.ExternalIds.Add(new ExternalId { ProviderName = dto.ProviderName, Value = dto.Value, MediaId = media.Id });
        }

        await metadataTagSyncService.ApplyTagsAsync(
            media,
            MetadataTagBuilder.FromManualUpdate(media, request.Genres, request.ContentRating, request.Network),
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyOverview(BaseMedia media, string overview)
    {
        switch (media)
        {
            case Movie m: m.Overview = overview; break;
            case Serie s: s.Overview = overview; break;
            case SerieSeason ss: ss.Overview = overview; break;
            case SerieEpisode se: se.Overview = overview; break;
            case MusicAlbum ma: ma.Overview = overview; break;
            case MusicArtist artist: artist.Biography = overview; break;
            default: break;
        }
    }
}
