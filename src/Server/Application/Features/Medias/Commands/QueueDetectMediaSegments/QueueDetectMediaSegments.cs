using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.DetectMediaSegments;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Medias.Commands.QueueDetectMediaSegments;

[Authorize(Roles = Roles.Administrator)]
public record QueueDetectMediaSegmentsCommand : IRequest
{
    public required Guid SeasonId { get; init; }
}

public class QueueDetectMediaSegmentsCommandHandler(IApplicationDbContext context, ISender sender)
    : IRequestHandler<QueueDetectMediaSegmentsCommand>
{
    public async Task Handle(QueueDetectMediaSegmentsCommand request, CancellationToken cancellationToken)
    {
        var season = await context.Medias
            .OfType<SerieSeason>()
            .FirstOrDefaultAsync(s => s.Id == request.SeasonId, cancellationToken);

        Guard.Against.NotFound(request.SeasonId, season);

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new DetectMediaSegmentsCommand { SeasonId = request.SeasonId },
            TargetEntityId = request.SeasonId,
            TargetEntityTypeName = nameof(SerieSeason),
            Lane = BackgroundTaskLane.MediaAnalysis,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.User,
            MaxAttempts = 2
        }, cancellationToken);
    }
}
