using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.StreamSessions;

public record CreateStreamSessionCommand : IRequest<StreamingSessionDto>
{
    public required Guid IndexedFileId { get; init; }
    public required Guid DeviceId { get; init; }
};

public class CreateStreamSessionCommandHandler : IRequestHandler<CreateStreamSessionCommand, StreamingSessionDto>
{
    private readonly IApplicationDbContext _context;

    public CreateStreamSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StreamingSessionDto> Handle(CreateStreamSessionCommand command, CancellationToken cancellationToken)
    {
        var indexedFile = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == command.IndexedFileId, cancellationToken);

        Guard.Against.NotFound(command.IndexedFileId, indexedFile);

        var session = new StreamingSessionDto
        {
            Id = Guid.NewGuid(),
            IndexedFileId = command.IndexedFileId,
            State = Domain.Enums.PlaybackState.Idle,
            Position = 0,
            PlaybackSettings = new PlaybackSettingsDto()
        };

        // For now we keep the session in memory only; later this can be persisted.
        // The Web layer is responsible for turning this into an HTTP response and
        // for optionally attaching HLS session-specific details.
        return session;
    }
}
