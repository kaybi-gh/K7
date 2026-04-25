using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.Home.Commands.DeleteDefaultHomeLayout;

[Authorize(Roles = Roles.Administrator)]
public record DeleteDefaultHomeLayoutCommand : IRequest;

public class DeleteDefaultHomeLayoutCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<DeleteDefaultHomeLayoutCommand>
{
    public async Task Handle(DeleteDefaultHomeLayoutCommand request, CancellationToken cancellationToken)
    {
        await serverSettingsService.RemoveAsync(ServerSettingKeys.HomeLayout, cancellationToken);
    }
}
