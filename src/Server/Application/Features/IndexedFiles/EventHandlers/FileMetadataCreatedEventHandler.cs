using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Features.IndexedFiles.Commands.GenerateThumbnails;
using K7.Server.Application.Features.Medias.Commands.DetectMediaSegments;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.EventHandlers;

public class FileMetadataCreatedEventHandler : INotificationHandler<FileMetadataCreatedEvent>
{
    private readonly ILogger<FileMetadataCreatedEventHandler> _logger;
    private readonly ISender _sender;
    private readonly IServerSettingsService _serverSettingsService;
    private readonly IApplicationDbContext _context;

    public FileMetadataCreatedEventHandler(
        ILogger<FileMetadataCreatedEventHandler> logger,
        ISender sender,
        IServerSettingsService serverSettingsService,
        IApplicationDbContext context)
    {
        _logger = logger;
        _sender = sender;
        _serverSettingsService = serverSettingsService;
        _context = context;
    }

    public async Task Handle(FileMetadataCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);

        var flags = await GetFeatureFlagsAsync(cancellationToken);

        if (flags.TransmuxingEnabled)
        {
            await _sender.Send(new CreateBackgroundTaskCommand()
            {
                Request = new ComputeHlsSegmentsCommand()
                {
                    Id = notification.IndexedFile.Id,
                    SegmentsDuration = TimeSpan.FromSeconds(2)
                },
                Priority = BackgroundTaskPriority.High,
                TargetEntityId = notification.IndexedFile.Id,
                TargetEntityTypeName = nameof(IndexedFile),
                MaxAttempts = 5,
                ConcurrencyGroup = "ffmpeg"
            }, cancellationToken);
        }

        if (notification.FileType == FileType.Video && flags.SeekbarThumbnailGenerationEnabled)
        {
            await _sender.Send(new CreateBackgroundTaskCommand()
            {
                Request = new GenerateThumbnailsCommand()
                {
                    Id = notification.IndexedFile.Id
                },
                Priority = BackgroundTaskPriority.Lowest,
                TargetEntityId = notification.IndexedFile.Id,
                TargetEntityTypeName = nameof(IndexedFile),
                MaxAttempts = 1,
                ConcurrencyGroup = "ffmpeg"
            }, cancellationToken);
        }

        if (notification.FileType == FileType.Video && flags.IntroDetectionEnabled)
        {
            await TriggerIntroDetectionIfEligibleAsync(notification.IndexedFile, cancellationToken);
        }
    }

    private async Task TriggerIntroDetectionIfEligibleAsync(IndexedFile indexedFile, CancellationToken cancellationToken)
    {
        if (indexedFile.MediaId is null)
            return;

        var episode = await _context.Medias
            .OfType<SerieEpisode>()
            .FirstOrDefaultAsync(e => e.Id == indexedFile.MediaId, cancellationToken);

        if (episode is null)
            return;

        var episodeCount = await _context.Medias
            .OfType<SerieEpisode>()
            .CountAsync(e => e.SeasonId == episode.SeasonId, cancellationToken);

        if (episodeCount < 2)
            return;

        await _sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new DetectMediaSegmentsCommand { SeasonId = episode.SeasonId },
            Priority = BackgroundTaskPriority.Low,
            TargetEntityId = episode.SeasonId,
            TargetEntityTypeName = nameof(SerieSeason),
            MaxAttempts = 2,
            ConcurrencyGroup = "ffmpeg"
        }, cancellationToken);
    }

    private async Task<ServerFeatureFlagsDto> GetFeatureFlagsAsync(CancellationToken cancellationToken)
    {
        var json = await _serverSettingsService.GetAsync(ServerSettingKeys.FeatureFlags, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<ServerFeatureFlagsDto>(json) ?? new ServerFeatureFlagsDto();

        return new ServerFeatureFlagsDto();
    }
}