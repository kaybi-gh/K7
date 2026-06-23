using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.MusicIntelligence.Commands.UpdateMusicIntelligenceSettings;

[Authorize(Roles = Roles.Administrator)]
public record UpdateMusicIntelligenceSettingsCommand : IRequest
{
    public required MusicIntelligenceSettingsDto Settings { get; init; }
}

public class UpdateMusicIntelligenceSettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<UpdateMusicIntelligenceSettingsCommand>
{
    public async Task Handle(UpdateMusicIntelligenceSettingsCommand request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request.Settings);
        await serverSettingsService.SetAsync(ServerSettingKeys.AudioMuseAi, json, cancellationToken);
    }
}
