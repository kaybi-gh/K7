using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Commands.UpdateScrobblingSettings;

[Authorize(Roles = Roles.Administrator)]
public record UpdateScrobblingSettingsCommand(ScrobblingSettingsDto Settings) : IRequest;

public class UpdateScrobblingSettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<UpdateScrobblingSettingsCommand>
{
    public async Task Handle(UpdateScrobblingSettingsCommand request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request.Settings);
        await serverSettingsService.SetAsync(ServerSettingKeys.Scrobbling, json, cancellationToken);
    }
}
