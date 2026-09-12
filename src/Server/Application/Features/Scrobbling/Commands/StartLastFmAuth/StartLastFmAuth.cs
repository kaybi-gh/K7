using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Commands.StartLastFmAuth;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record StartLastFmAuthCommand : IRequest<LastFmAuthStartDto>;

public class StartLastFmAuthCommandHandler(LastFmClient lastFmClient)
    : IRequestHandler<StartLastFmAuthCommand, LastFmAuthStartDto>
{
    public Task<LastFmAuthStartDto> Handle(StartLastFmAuthCommand request, CancellationToken cancellationToken) =>
        lastFmClient.StartAuthAsync(cancellationToken);
}
