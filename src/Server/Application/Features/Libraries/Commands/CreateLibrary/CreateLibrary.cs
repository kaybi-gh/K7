using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;
using K7.Server.Application.Services;
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
    public bool IntroDetectionEnabled { get; init; } = true;
    public bool ThemeSongGenerationEnabled { get; init; } = true;
    public bool SeekbarThumbnailGenerationEnabled { get; init; } = true;
    public bool ChapterExtractionEnabled { get; init; } = true;
    public bool MusicAudioAnalysisEnabled { get; init; } = true;
    public bool TranscodingEnabled { get; init; } = true;
    public bool TransmuxingEnabled { get; init; } = true;
    public int? MetadataRefreshIntervalDays { get; init; }
    public bool RealtimeMonitorEnabled { get; init; } = true;
    public int AutoScanIntervalHours { get; init; } = 6;
}

public class CreateLibraryCommandHandler : IRequestHandler<CreateLibraryCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly ILibraryFolderWatcher _libraryFolderWatcher;

    public CreateLibraryCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        ILibraryFolderWatcher libraryFolderWatcher)
    {
        _context = context;
        _sender = sender;
        _libraryFolderWatcher = libraryFolderWatcher;
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
            LibraryGroupId = libraryGroupId,
            IntroDetectionEnabled = request.IntroDetectionEnabled,
            ThemeSongGenerationEnabled = request.IntroDetectionEnabled && request.ThemeSongGenerationEnabled,
            SeekbarThumbnailGenerationEnabled = request.SeekbarThumbnailGenerationEnabled,
            ChapterExtractionEnabled = request.ChapterExtractionEnabled,
            MusicAudioAnalysisEnabled = request.MusicAudioAnalysisEnabled,
            TranscodingEnabled = request.TranscodingEnabled,
            TransmuxingEnabled = request.TransmuxingEnabled,
            MetadataRefreshIntervalDays = request.MetadataRefreshIntervalDays,
            RealtimeMonitorEnabled = request.RealtimeMonitorEnabled,
            AutoScanIntervalHours = request.AutoScanIntervalHours
        };

        entity.AddDomainEvent(new LibraryCreatedEvent(entity));
        _context.Libraries.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await _libraryFolderWatcher.RefreshWatchersAsync(cancellationToken);

        if (request.TriggerFileIndexingOnCreation)
        {
            await _sender.Send(new CreateBackgroundTaskCommand()
            {
                Request = new IndexLibraryFilesCommand(entity.Id),
                Priority = BackgroundTaskPriority.Normal,
                TargetEntityId = entity.Id,
                TargetEntityTypeName = nameof(Library),
                TimeoutSeconds = 3600,
                ConcurrencyGroup = "library-scan"
            }, cancellationToken);
        }

        return entity.Id;
    }
}
