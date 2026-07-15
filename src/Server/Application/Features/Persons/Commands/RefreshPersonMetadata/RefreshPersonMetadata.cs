using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Persons.Commands.RefreshPersonMetadata;

public record RefreshPersonMetadataCommand : IRequest
{
    public required Guid PersonId { get; init; }
    public required string ProviderName { get; init; }
    public required string ProviderId { get; init; }
    public required string Language { get; init; }
}

public class RefreshPersonMetadataCommandHandler(
    IApplicationDbContext context,
    IEnumerable<IPersonMetadataProvider> providers,
    ILogger<RefreshPersonMetadataCommandHandler> logger)
    : IRequestHandler<RefreshPersonMetadataCommand>
{
    private readonly IReadOnlyDictionary<string, IPersonMetadataProvider> _providers =
        providers.ToDictionary(p => p.ProviderName);

    public async Task Handle(RefreshPersonMetadataCommand request, CancellationToken cancellationToken)
    {
        var person = await context.Persons
            .Include(p => p.ExternalIds)
            .Include(p => p.PortraitPicture)
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken);

        Guard.Against.NotFound(request.PersonId, person);

        if (!_providers.TryGetValue(request.ProviderName, out var provider))
        {
            logger.LogWarning(
                "Skipping person metadata refresh for {PersonId}: provider {ProviderName} is not registered",
                request.PersonId,
                request.ProviderName);
            return;
        }

        var details = await provider.FetchPersonAsync(request.ProviderId, request.Language, cancellationToken);
        if (details is null)
        {
            logger.LogWarning(
                "Skipping person metadata refresh for {PersonId}: provider {ProviderName} returned no details for {ProviderId}",
                request.PersonId,
                request.ProviderName,
                request.ProviderId);
            return;
        }

        if (!string.IsNullOrEmpty(details.Biography))
            person.Biography = details.Biography;

        if (details.Birthday.HasValue)
            person.Birthday = details.Birthday;

        if (details.Deathday.HasValue)
            person.Deathday = details.Deathday;

        if (!string.IsNullOrEmpty(details.BirthPlace))
            person.BirthPlace = details.BirthPlace;

        if (details.Gender != PersonGender.NotSpecified)
            person.Gender = details.Gender;

        if (!string.IsNullOrEmpty(details.ImageUrl)
            && MetadataImageUrlHelper.TryCreateRemoteUri(details.ImageUrl, out var imageUri))
        {
            if (person.PortraitPicture is null || person.PortraitPicture.OriginalRemoteUri != imageUri)
            {
                var picture = new MetadataPicture
                {
                    OriginalRemoteUri = imageUri,
                    Type = MetadataPictureType.Portrait
                };
                picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
                person.PortraitPicture = picture;
            }
        }

        foreach (var entry in details.AdditionalExternalIds)
        {
            if (!person.ExternalIds.Any(e => e.ProviderName == entry.ProviderName))
                person.ExternalIds.Add(new ExternalId { ProviderName = entry.ProviderName, Value = entry.Value, PersonId = person.Id });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
