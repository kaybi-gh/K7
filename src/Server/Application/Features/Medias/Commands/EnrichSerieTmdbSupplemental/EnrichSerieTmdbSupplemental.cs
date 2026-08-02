using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.GenerateEpisodeStillFromSource;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application.Features.Medias.Commands.EnrichSerieTmdbSupplemental;

public record EnrichSerieTmdbSupplementalCommand : IRequest
{
    public required Guid MediaId { get; init; }
    public required string Language { get; init; }
    public required string FallbackLanguage { get; init; }
}

public class EnrichSerieTmdbSupplementalCommandHandler(
    IApplicationDbContext context,
    IServiceProvider serviceProvider,
    ISender sender,
    SerieSupplementalCastEnrichmentService supplementalCastEnrichmentService)
    : IRequestHandler<EnrichSerieTmdbSupplementalCommand>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ISender _sender = sender;
    private readonly SerieSupplementalCastEnrichmentService _supplementalCastEnrichmentService = supplementalCastEnrichmentService;

    public async Task Handle(EnrichSerieTmdbSupplementalCommand request, CancellationToken cancellationToken)
    {
        var serie = await _context.Medias
            .OfType<Serie>()
            .Include(s => s.ExternalIds)
            .Include(s => s.PersonRoles)
                .ThenInclude(pr => pr.Person)
                    .ThenInclude(p => p.ExternalIds)
            .Include(s => s.Ratings)
            .FirstOrDefaultAsync(s => s.Id == request.MediaId, cancellationToken);
        Guard.Against.NotFound(request.MediaId, serie);

        await _context.Entry(serie).Collection(s => s.Seasons).Query()
            .Include(s => s.ExternalIds)
            .Include(s => s.Episodes).ThenInclude(e => e.ExternalIds)
            .Include(s => s.Episodes).ThenInclude(e => e.Pictures)
            .Include(s => s.Episodes).ThenInclude(e => e.Ratings)
            .Include(s => s.Episodes).ThenInclude(e => e.PersonRoles)
                .ThenInclude(pr => pr.PortraitPicture!)
                    .ThenInclude(p => p.Variants)
            .LoadAsync(cancellationToken);

        var tmdbSerieProvider = _serviceProvider.GetKeyedService<ISerieMetadataProvider>("tmdb");
        if (tmdbSerieProvider is null)
            return;

        var supplementalSerieMetadata = await SupplementalEpisodeMetadataResolver.TryFetchTmdbSerieMetadataAsync(
            tmdbSerieProvider,
            serie,
            request.Language,
            request.FallbackLanguage,
            cancellationToken);

        SupplementalEpisodeMetadataResolver.MergeMetadataProviderRatings(
            serie,
            supplementalSerieMetadata?.Ratings);

        if (!serie.IsFieldLocked(nameof(Serie.PersonRoles))
            && supplementalSerieMetadata?.PersonRoles is { Count: > 0 })
        {
            await _supplementalCastEnrichmentService.EnrichFromSupplementalAsync(
                serie,
                supplementalSerieMetadata.PersonRoles.ToList(),
                request.Language,
                cancellationToken);
        }

        foreach (var season in serie.Seasons)
        {
            foreach (var episode in season.Episodes)
            {
                var supplementalMetadata = await SupplementalEpisodeMetadataResolver.TryFetchTmdbEpisodeMetadataAsync(
                    tmdbSerieProvider,
                    serie,
                    season.SeasonNumber,
                    episode.EpisodeNumber,
                    request.Language,
                    request.FallbackLanguage,
                    cancellationToken);

                if (supplementalMetadata is null)
                    continue;

                SupplementalEpisodeMetadataResolver.MergeMetadataProviderRatings(
                    episode,
                    supplementalMetadata.Ratings);

                SupplementalEpisodeMetadataResolver.MergeSupplementalExternalIds(
                    episode,
                    supplementalMetadata.ExternalIds);

                var stillImageUrl = supplementalMetadata.StillImageUrl;
                if (!string.IsNullOrEmpty(stillImageUrl)
                    && !episode.IsPictureTypeLocked(MetadataPictureType.Still)
                    && MetadataImageUrlHelper.TryCreateRemoteUri(stillImageUrl, out var stillUri))
                {
                    var stillPicture = new MetadataPicture
                    {
                        OriginalRemoteUri = stillUri,
                        Type = MetadataPictureType.Still
                    };
                    stillPicture.AddDomainEvent(new MetadataPictureCreatedEvent(stillPicture));

                    episode.RemovePicturesOfType(MetadataPictureType.Still);
                    episode.Pictures.Add(stillPicture);
                }
                else if (!episode.IsPictureTypeLocked(MetadataPictureType.Still)
                         && string.IsNullOrWhiteSpace(supplementalMetadata.StillImageUrl))
                {
                    await TryQueueEpisodeStillFromSourceFallbackAsync(episode, cancellationToken);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task TryQueueEpisodeStillFromSourceFallbackAsync(
        SerieEpisode episode,
        CancellationToken cancellationToken)
    {
        if (episode.Pictures.Any(picture => picture.Type == MetadataPictureType.Still))
            return;

        var hasIndexedVideo = await _context.IndexedFiles
            .AnyAsync(
                file => file.MediaId == episode.Id && file.FileMetadata is VideoFileMetadata,
                cancellationToken);

        if (!hasIndexedVideo)
            return;

        await _sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new GenerateEpisodeStillFromSourceCommand { MediaId = episode.Id },
            TargetEntityId = episode.Id,
            TargetEntityTypeName = nameof(SerieEpisode),
            Lane = BackgroundTaskLane.ImageExtract,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            MaxAttempts = 2
        }, cancellationToken);
    }
}
