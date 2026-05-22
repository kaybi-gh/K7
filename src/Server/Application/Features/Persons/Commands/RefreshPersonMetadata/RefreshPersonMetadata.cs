using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;

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
    IEnumerable<IPersonMetadataProvider> providers)
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

        if (!_providers.TryGetValue(request.ProviderName, out var provider)) return;

        var details = await provider.FetchPersonAsync(request.ProviderId, request.Language, cancellationToken);
        if (details is null) return;

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

        if (!string.IsNullOrEmpty(details.ImageUrl))
        {
            var imageUri = new Uri(details.ImageUrl);
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
