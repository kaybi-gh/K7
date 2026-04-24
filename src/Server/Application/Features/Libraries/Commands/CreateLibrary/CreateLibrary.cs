using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Libraries.Commands.CreateLibrary;

[Authorize(Roles = Roles.Administrator)]
public record CreateLibraryCommand : IRequest<Guid>
{
    public required string Title { get; init; }
    public required LibraryMediaType MediaType { get; init; }
    public required string RootPath { get; init; }
    public required string MetadataProviderName { get; init; }
    public bool TriggerFileIndexingOnCreation { get; init; } = true;
    public string? Description { get; init; }
    public string? Icon { get; init; }
}

public class CreateLibraryCommandHandler : IRequestHandler<CreateLibraryCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public CreateLibraryCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<Guid> Handle(CreateLibraryCommand request, CancellationToken cancellationToken)
    {
        var entity = new Library
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            MediaType = request.MediaType,
            RootPath = request.RootPath,
            MetadataProviderName = request.MetadataProviderName,
            Description = request.Description,
            Icon = request.Icon
        };

        entity.AddDomainEvent(new LibraryCreatedEvent(entity));
        _context.Libraries.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        if (request.TriggerFileIndexingOnCreation)
        {
            await _sender.Send(new CreateBackgroundTaskCommand()
            {
                Request = new IndexLibraryFilesCommand(entity.Id),
                Priority = BackgroundTaskPriority.Normal,
                TargetEntityId = entity.Id,
                TargetEntityTypeName = nameof(Library),
            }, cancellationToken);
        }

        return entity.Id;
    }
}
