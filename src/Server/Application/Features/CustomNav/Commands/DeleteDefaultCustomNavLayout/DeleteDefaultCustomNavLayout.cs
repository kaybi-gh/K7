using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.CustomNav.Commands.DeleteDefaultCustomNavLayout;

[Authorize(Roles = Roles.Administrator)]
public record DeleteDefaultCustomNavLayoutCommand : IRequest;

public class DeleteDefaultCustomNavLayoutCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<DeleteDefaultCustomNavLayoutCommand>
{
    public async Task Handle(DeleteDefaultCustomNavLayoutCommand request, CancellationToken cancellationToken)
    {
        await serverSettingsService.RemoveAsync(ServerSettingKeys.CustomNavLayout, cancellationToken);
    }
}
