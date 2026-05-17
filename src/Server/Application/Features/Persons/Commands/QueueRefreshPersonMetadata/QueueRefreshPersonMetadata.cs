using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Persons.Commands.RefreshPersonMetadata;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Persons.Commands.QueueRefreshPersonMetadata;

[Authorize(Roles = Roles.Administrator)]
public record QueueRefreshPersonMetadataCommand : IRequest
{
    public required Guid PersonId { get; init; }
}

public class QueueRefreshPersonMetadataCommandHandler(IApplicationDbContext context, ISender sender)
    : IRequestHandler<QueueRefreshPersonMetadataCommand>
{
    public async Task Handle(QueueRefreshPersonMetadataCommand request, CancellationToken cancellationToken)
    {
        var person = await context.Persons
            .Include(p => p.ExternalIds)
            .Include(p => p.Roles)
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken);

        Guard.Against.NotFound(request.PersonId, person);

        var externalId = person.ExternalIds.FirstOrDefault(e => e.ProviderName == "tmdb");
        Guard.Against.NotFound(request.PersonId, externalId, $"Person {request.PersonId} has no TMDb external ID.");

        // Find language from a library associated with this person's media roles
        var mediaId = person.Roles.Select(r => r.MediaId).FirstOrDefault();
        var library = mediaId != default
            ? await context.Libraries
                .FirstOrDefaultAsync(l => l.IndexedFiles.Any(f => f.MediaId == mediaId), cancellationToken)
            : null;

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new RefreshPersonMetadataCommand
            {
                PersonId = person.Id,
                ProviderName = externalId.ProviderName,
                ProviderId = externalId.Value,
                Language = library?.MetadataLanguage ?? "en"
            },
            Priority = BackgroundTaskPriority.High,
            TargetEntityId = person.Id,
            TargetEntityTypeName = nameof(Person),
            MaxAttempts = 1,
            ConcurrencyGroup = "tmdb"
        }, cancellationToken);
    }
}
