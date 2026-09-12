using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Commands.StartTraktDevice;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record StartTraktDeviceCommand : IRequest<TraktDeviceStartDto>;

public class StartTraktDeviceCommandHandler(TraktClient traktClient)
    : IRequestHandler<StartTraktDeviceCommand, TraktDeviceStartDto>
{
    public Task<TraktDeviceStartDto> Handle(StartTraktDeviceCommand request, CancellationToken cancellationToken) =>
        traktClient.StartDeviceAsync(cancellationToken);
}
