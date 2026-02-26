using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.StreamSessions.Commands.CreateStreamSession;

public record CreateStreamSessionCommand : IRequest<StreamingSessionDto>
{
    public required Guid IndexedFileId { get; init; }
    public required Guid DeviceId { get; init; }
    public int? AudioTrackIndex { get; init; }
};

public class CreateStreamSessionCommandHandler : IRequestHandler<CreateStreamSessionCommand, StreamingSessionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public CreateStreamSessionCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<StreamingSessionDto> Handle(CreateStreamSessionCommand command, CancellationToken cancellationToken)
    {
        var indexedFile = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == command.IndexedFileId, cancellationToken);

        Guard.Against.NotFound(command.IndexedFileId, indexedFile);

        var device = await _context.Devices.FindAsync([command.DeviceId], cancellationToken);
        Guard.Against.NotFound(command.DeviceId, device);

        var playbackSettings = new PlaybackSettingsDto();

        var session = new StreamSession
        {
            Id = Guid.NewGuid(),
            IndexedFileId = command.IndexedFileId,
            DeviceId = command.DeviceId,
            UserId = _user.Id != null ? Guid.Parse(_user.Id) : null,
            State = Domain.Enums.PlaybackState.Idle,
            Position = 0,
            PlaybackSettingsJson = JsonSerializer.Serialize(playbackSettings)
        };

        _context.StreamSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return new StreamingSessionDto
        {
            Id = session.Id,
            IndexedFileId = session.IndexedFileId,
            State = session.State,
            Position = session.Position,
            PlaybackSettings = playbackSettings
        };
    }
}
