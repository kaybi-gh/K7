using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.AudioPlayerSettings.Queries.GetEffectiveAudioPlayerSettings;

public record GetEffectiveAudioPlayerSettingsQuery : IRequest<AudioPlayerSettingsDto>;

public class GetEffectiveAudioPlayerSettingsQueryHandler(
    IUserSettingsService userSettingsService,
    IServerSettingsService serverSettingsService,
    IUser currentUser)
    : IRequestHandler<GetEffectiveAudioPlayerSettingsQuery, AudioPlayerSettingsDto>
{
    public async Task<AudioPlayerSettingsDto> Handle(GetEffectiveAudioPlayerSettingsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is { } userId)
        {
            var userJson = await userSettingsService.GetAsync(userId, UserSettingKeys.AudioPlayerSettings, cancellationToken);
            if (userJson is not null)
                return JsonSerializer.Deserialize<AudioPlayerSettingsDto>(userJson) ?? new AudioPlayerSettingsDto();
        }

        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.AudioPlayerSettings, cancellationToken);
        if (serverJson is not null)
            return JsonSerializer.Deserialize<AudioPlayerSettingsDto>(serverJson) ?? new AudioPlayerSettingsDto();

        return new AudioPlayerSettingsDto();
    }
}
