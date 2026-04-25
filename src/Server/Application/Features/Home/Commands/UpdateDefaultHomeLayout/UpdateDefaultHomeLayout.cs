using System.Text.Json;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.Home.Commands.UpdateDefaultHomeLayout;

[Authorize(Roles = Roles.Administrator)]
public record UpdateDefaultHomeLayoutCommand : IRequest
{
    public required HomeLayoutDto Layout { get; init; }
}

public class UpdateDefaultHomeLayoutCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<UpdateDefaultHomeLayoutCommand>
{
    public async Task Handle(UpdateDefaultHomeLayoutCommand request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request.Layout);
        await serverSettingsService.SetAsync(ServerSettingKeys.HomeLayout, json, cancellationToken);
    }
}
