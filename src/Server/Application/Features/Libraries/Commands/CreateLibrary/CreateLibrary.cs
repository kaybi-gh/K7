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
    public required string MetadataLanguage { get; init; }
    public required string MetadataFallbackLanguage { get; init; }
    public bool TriggerFileIndexingOnCreation { get; init; } = true;
    public Guid? LibraryGroupId { get; init; }
    public string? GroupTitle { get; init; }
    public string? GroupDescription { get; init; }
    public string? GroupIcon { get; init; }
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
        Guid libraryGroupId;

        if (request.LibraryGroupId.HasValue)
        {
            var group = await _context.LibraryGroups
                .FindAsync([request.LibraryGroupId.Value], cancellationToken);
            Guard.Against.NotFound(request.LibraryGroupId.Value, group);
            libraryGroupId = group.Id;
        }
        else
        {
            var group = new LibraryGroup
            {
                Id = Guid.NewGuid(),
                Title = request.GroupTitle ?? request.Title,
                MediaType = request.MediaType,
                Description = request.GroupDescription,
                Icon = request.GroupIcon
            };
            _context.LibraryGroups.Add(group);
            libraryGroupId = group.Id;
        }

        var entity = new Library
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            MediaType = request.MediaType,
            RootPath = request.RootPath,
            MetadataProviderName = request.MetadataProviderName,
            MetadataLanguage = request.MetadataLanguage,
            MetadataFallbackLanguage = request.MetadataFallbackLanguage,
            LibraryGroupId = libraryGroupId
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
                TimeoutSeconds = 3600
            }, cancellationToken);
        }

        return entity.Id;
    }
}
