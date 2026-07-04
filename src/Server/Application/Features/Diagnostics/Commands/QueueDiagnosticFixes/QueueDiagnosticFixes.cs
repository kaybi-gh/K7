using FluentValidation.Results;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.Diagnostics.Commands.FixDiagnosticItems;
using K7.Server.Application.Features.Diagnostics.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Diagnostics.Commands.QueueDiagnosticFixes;

[Authorize(Roles = Roles.Administrator)]
public record QueueDiagnosticFixesCommand : IRequest<int>
{
    public required DiagnosticIssue Issue { get; init; }
    public Guid? LibraryId { get; init; }
}

public class QueueDiagnosticFixesCommandHandler(
    DiagnosticIssueEntityResolver entityResolver,
    DiagnosticFixBatchBuilder batchBuilder,
    ISender sender,
    ILogger<QueueDiagnosticFixesCommandHandler> logger)
    : IRequestHandler<QueueDiagnosticFixesCommand, int>
{
    private const int BatchSize = 500;
    private const int MetadataFixChunkSize = 100;

    public async Task<int> Handle(QueueDiagnosticFixesCommand request, CancellationToken cancellationToken)
    {
        var action = DiagnosticFixMappings.GetFixAction(request.Issue)
            ?? throw new ValidationException(
            [
                new ValidationFailure(nameof(request.Issue), $"Issue {request.Issue} does not support automated fixes.")
            ]);

        var entityIds = await entityResolver.ResolveEntityIdsAsync(request.Issue, request.LibraryId, cancellationToken);
        if (entityIds.Count == 0)
            return 0;

        var queued = 0;

        if (DiagnosticFixBatchBuilder.UsesBackgroundTaskBatch(action))
        {
            foreach (var batch in entityIds.Chunk(BatchSize))
            {
                var items = await batchBuilder.BuildBatchItemsAsync(action, batch, cancellationToken);
                if (items.Count == 0)
                    continue;

                await sender.Send(new CreateBackgroundTasksBatchCommand(items), cancellationToken);
                queued += items.Count;
            }
        }
        else
        {
            foreach (var batch in entityIds.Chunk(MetadataFixChunkSize))
            {
                queued += await sender.Send(new FixDiagnosticItemsCommand
                {
                    EntityIds = batch,
                    Action = action
                }, cancellationToken);
            }
        }

        logger.LogInformation(
            "Queued diagnostic fix {Action} for {QueuedCount} entities (issue: {Issue}, library: {LibraryId})",
            action, queued, request.Issue, request.LibraryId);

        return queued;
    }
}
