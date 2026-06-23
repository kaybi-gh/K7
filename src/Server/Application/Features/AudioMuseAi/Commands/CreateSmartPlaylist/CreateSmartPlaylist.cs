using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.AudioMuseAi.Commands.CreateSmartPlaylist;

[Authorize(Roles = Roles.User)]
public record CreateSmartPlaylistCommand : IRequest<List<Guid>>
{
    public required string Prompt { get; init; }
    public int Count { get; init; } = 30;
}

public class CreateSmartPlaylistCommandHandler(IAudioMuseAiService audioMuseAiService)
    : IRequestHandler<CreateSmartPlaylistCommand, List<Guid>>
{
    public async Task<List<Guid>> Handle(CreateSmartPlaylistCommand request, CancellationToken cancellationToken)
    {
        return await audioMuseAiService.CreateSmartPlaylistAsync(request.Prompt, request.Count, cancellationToken);
    }
}
