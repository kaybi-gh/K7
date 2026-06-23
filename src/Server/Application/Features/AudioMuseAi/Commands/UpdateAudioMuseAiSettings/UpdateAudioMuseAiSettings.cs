using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.AudioMuseAi.Commands.UpdateAudioMuseAiSettings;

[Authorize(Roles = Roles.Administrator)]
public record UpdateAudioMuseAiSettingsCommand : IRequest
{
    public required AudioMuseAiSettingsDto Settings { get; init; }
}

public class UpdateAudioMuseAiSettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<UpdateAudioMuseAiSettingsCommand>
{
    public async Task Handle(UpdateAudioMuseAiSettingsCommand request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request.Settings);
        await serverSettingsService.SetAsync(ServerSettingKeys.AudioMuseAi, json, cancellationToken);

        var flagsJson = await serverSettingsService.GetAsync(ServerSettingKeys.FeatureFlags, cancellationToken);
        var flags = flagsJson is not null
            ? JsonSerializer.Deserialize<ServerFeatureFlagsDto>(flagsJson) ?? new ServerFeatureFlagsDto()
            : new ServerFeatureFlagsDto();

        var updatedFlags = flags with { AudioMuseAiEnabled = request.Settings.Enabled };
        await serverSettingsService.SetAsync(ServerSettingKeys.FeatureFlags, JsonSerializer.Serialize(updatedFlags), cancellationToken);
    }
}
