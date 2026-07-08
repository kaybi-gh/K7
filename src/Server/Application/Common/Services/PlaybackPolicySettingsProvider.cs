using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Services;

public interface IPlaybackPolicySettingsProvider
{
    Task<VideoPlaybackPolicySettingsDto> GetEffectiveVideoPolicyAsync(
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<AudioPlaybackPolicySettingsDto> GetEffectiveAudioPolicyAsync(
        Guid? userId,
        CancellationToken cancellationToken = default);
}

public class PlaybackPolicySettingsProvider(
    IUserSettingsService userSettingsService,
    IServerSettingsService serverSettingsService)
    : IPlaybackPolicySettingsProvider
{
    public async Task<VideoPlaybackPolicySettingsDto> GetEffectiveVideoPolicyAsync(
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (userId is { } id)
        {
            var userJson = await userSettingsService.GetAsync(id, UserSettingKeys.VideoPlaybackPolicy, cancellationToken);
            if (userJson is not null)
                return JsonSerializer.Deserialize<VideoPlaybackPolicySettingsDto>(userJson) ?? new VideoPlaybackPolicySettingsDto();
        }

        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.VideoPlaybackPolicy, cancellationToken);
        if (serverJson is not null)
            return JsonSerializer.Deserialize<VideoPlaybackPolicySettingsDto>(serverJson) ?? new VideoPlaybackPolicySettingsDto();

        return new VideoPlaybackPolicySettingsDto();
    }

    public async Task<AudioPlaybackPolicySettingsDto> GetEffectiveAudioPolicyAsync(
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (userId is { } id)
        {
            var userJson = await userSettingsService.GetAsync(id, UserSettingKeys.AudioPlaybackPolicy, cancellationToken);
            if (userJson is not null)
                return JsonSerializer.Deserialize<AudioPlaybackPolicySettingsDto>(userJson) ?? new AudioPlaybackPolicySettingsDto();
        }

        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.AudioPlaybackPolicy, cancellationToken);
        if (serverJson is not null)
            return JsonSerializer.Deserialize<AudioPlaybackPolicySettingsDto>(serverJson) ?? new AudioPlaybackPolicySettingsDto();

        return new AudioPlaybackPolicySettingsDto();
    }
}
