using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using MediatR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.StreamSessions.Commands.CreateStreamSession;

public record CreateStreamSessionCommand : IRequest<StreamingSessionDto>
{
    public required Guid IndexedFileId { get; init; }
    public required Guid DeviceId { get; init; }
    public int? AudioTrackIndex { get; init; }
};

public class CreateStreamSessionCommandHandler(
    IApplicationDbContext context,
    IUser user,
    IMediaAccessGuard accessGuard,
    ISender sender,
    ILogger<CreateStreamSessionCommandHandler> logger) : IRequestHandler<CreateStreamSessionCommand, StreamingSessionDto>
{
    public async Task<StreamingSessionDto> Handle(CreateStreamSessionCommand command, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessByIndexedFileAsync(command.IndexedFileId, cancellationToken);
        var indexedFile = await context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == command.IndexedFileId, cancellationToken);

        Guard.Against.NotFound(command.IndexedFileId, indexedFile);

        var device = await context.Devices.FindAsync([command.DeviceId], cancellationToken);
        Guard.Against.NotFound(command.DeviceId, device);

        if (indexedFile.FileMetadata is VideoFileMetadata videoMetadata
            && ChapterExtractionHelper.NeedsExtraction(videoMetadata))
        {
            await ChapterExtractionHelper.EnsureChaptersAsync(
                context, sender, command.IndexedFileId, logger, cancellationToken);
            await context.Entry(indexedFile).Reference(f => f.FileMetadata).LoadAsync(cancellationToken);
        }

        var playbackSettings = new PlaybackSettingsDto();

        var session = new StreamSession
        {
            Id = Guid.NewGuid(),
            IndexedFileId = command.IndexedFileId,
            DeviceId = command.DeviceId,
            UserId = user.Id,
            State = Domain.Enums.PlaybackState.Idle,
            Position = 0,
            PlaybackSettingsJson = JsonSerializer.Serialize(playbackSettings)
        };

        context.StreamSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);

        IReadOnlyList<ChapterMarkerDto>? chapters = null;
        if (indexedFile.FileMetadata is VideoFileMetadata vfm && vfm.Chapters is not null)
        {
            chapters = vfm.Chapters.Select(c => new ChapterMarkerDto
            {
                StartSeconds = c.StartSeconds,
                EndSeconds = c.EndSeconds,
                Title = c.Title
            }).ToList();
        }

        return new StreamingSessionDto
        {
            Id = session.Id,
            IndexedFileId = session.IndexedFileId!.Value,
            State = session.State,
            Position = session.Position,
            PlaybackSettings = playbackSettings,
            Chapters = chapters
        };
    }
}
