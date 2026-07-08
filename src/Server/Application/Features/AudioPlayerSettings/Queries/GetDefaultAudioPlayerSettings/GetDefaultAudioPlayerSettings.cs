using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.AudioPlayerSettings.Queries.GetDefaultAudioPlayerSettings;

public record GetDefaultAudioPlayerSettingsQuery : IRequest<AudioPlayerSettingsDto?>;

public class GetDefaultAudioPlayerSettingsQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetDefaultAudioPlayerSettingsQuery, AudioPlayerSettingsDto?>
{
    public async Task<AudioPlayerSettingsDto?> Handle(GetDefaultAudioPlayerSettingsQuery request, CancellationToken cancellationToken)
    {
        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.AudioPlayerSettings, cancellationToken);
        return serverJson is not null
            ? JsonSerializer.Deserialize<AudioPlayerSettingsDto>(serverJson) ?? new AudioPlayerSettingsDto()
            : null;
    }
}
