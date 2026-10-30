using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Persons.Commands.RefreshPersonMetadata;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Queues TMDb person enrichment (biography, birthday, imdb id) for persons created from a
/// credits payload. Persons must be saved first so they carry an id.
/// </summary>
public static class PersonMetadataRefreshQueueHelper
{
    public static async Task QueueMissingTmdbDetailsAsync(
        ISender sender,
        IEnumerable<Person> persons,
        string language,
        CancellationToken cancellationToken = default)
    {
        foreach (var person in persons.Distinct())
        {
            if (person.Id == Guid.Empty || !PersonMetadataMergeHelper.NeedsProviderRefresh(person))
                continue;

            var tmdbId = person.ExternalIds.FirstOrDefault(e => e.ProviderName == "tmdb")?.Value;
            if (string.IsNullOrWhiteSpace(tmdbId))
                continue;

            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new RefreshPersonMetadataCommand
                {
                    PersonId = person.Id,
                    ProviderName = "tmdb",
                    ProviderId = tmdbId,
                    Language = language
                },
                TargetEntityId = person.Id,
                TargetEntityTypeName = nameof(Person),
                Lane = BackgroundTaskLane.Metadata,
                MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName("tmdb"),
                WorkClass = BackgroundTaskWorkClass.Polish,
                TriggeredBy = BackgroundTaskTriggeredBy.System,
                MaxAttempts = 3
            }, cancellationToken);
        }
    }
}
