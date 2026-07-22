using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Common.Services;

public static class PersonMetadataMergeHelper
{
    public static void MergeMissingPersonData(Person target, Person source)
    {
        if (!target.IsFieldLocked(nameof(Person.Biography))
            && string.IsNullOrWhiteSpace(target.Biography)
            && !string.IsNullOrWhiteSpace(source.Biography))
        {
            target.Biography = source.Biography;
        }

        if (!target.IsFieldLocked(nameof(Person.Birthday))
            && target.Birthday is null
            && source.Birthday is not null)
        {
            target.Birthday = source.Birthday;
        }

        if (!target.IsFieldLocked(nameof(Person.Deathday))
            && target.Deathday is null
            && source.Deathday is not null)
        {
            target.Deathday = source.Deathday;
        }

        if (!target.IsFieldLocked(nameof(Person.BirthPlace))
            && string.IsNullOrWhiteSpace(target.BirthPlace)
            && !string.IsNullOrWhiteSpace(source.BirthPlace))
        {
            target.BirthPlace = source.BirthPlace;
        }

        if (!target.IsFieldLocked(nameof(Person.Gender))
            && target.Gender == PersonGender.NotSpecified
            && source.Gender != PersonGender.NotSpecified)
        {
            target.Gender = source.Gender;
        }

        MergeMissingExternalIds(target, source.ExternalIds);

        if (target.PortraitPicture is null
            && source.PortraitPicture?.OriginalRemoteUri is not null)
        {
            var picture = new MetadataPicture
            {
                OriginalRemoteUri = source.PortraitPicture.OriginalRemoteUri,
                Type = MetadataPictureType.Portrait
            };
            picture.AddDomainEvent(new MetadataPictureCreatedEvent(picture));
            target.PortraitPicture = picture;
        }
    }

    public static void MergeMissingExternalIds(Person target, IEnumerable<ExternalId> sourceIds)
    {
        foreach (var sourceId in sourceIds)
        {
            if (string.IsNullOrWhiteSpace(sourceId.ProviderName) || string.IsNullOrWhiteSpace(sourceId.Value))
                continue;

            if (target.ExternalIds.Any(e =>
                    string.Equals(e.ProviderName, sourceId.ProviderName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            target.ExternalIds.Add(new ExternalId
            {
                ProviderName = sourceId.ProviderName,
                Value = sourceId.Value,
                PersonId = target.Id == Guid.Empty ? null : target.Id
            });
        }
    }

    public static bool NeedsProviderRefresh(Person person, string preferredProvider = "tmdb") =>
        person.ExternalIds.Any(e => string.Equals(e.ProviderName, preferredProvider, StringComparison.OrdinalIgnoreCase))
        && string.IsNullOrWhiteSpace(person.Biography)
        && person.Birthday is null;
}
