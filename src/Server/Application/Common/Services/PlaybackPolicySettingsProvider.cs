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

    Task<VideoPlaybackPolicySettingsDto> GetEffectiveVideoPolicyAsync(
        Guid? userId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken = default);

    Task<AudioPlaybackPolicySettingsDto> GetEffectiveAudioPolicyAsync(
        Guid? userId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken = default);
}

public class PlaybackPolicySettingsProvider(
    IUserSettingsService userSettingsService,
    ISharedProfileSettingsService sharedProfileSettingsService,
    IServerSettingsService serverSettingsService)
    : IPlaybackPolicySettingsProvider
{
    public Task<VideoPlaybackPolicySettingsDto> GetEffectiveVideoPolicyAsync(
        Guid? userId,
        CancellationToken cancellationToken = default) =>
        GetEffectiveVideoPolicyAsync(userId, sharedProfileId: null, cancellationToken);

    public Task<AudioPlaybackPolicySettingsDto> GetEffectiveAudioPolicyAsync(
        Guid? userId,
        CancellationToken cancellationToken = default) =>
        GetEffectiveAudioPolicyAsync(userId, sharedProfileId: null, cancellationToken);

    public async Task<VideoPlaybackPolicySettingsDto> GetEffectiveVideoPolicyAsync(
        Guid? userId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken = default)
    {
        if (sharedProfileId is { } profileId)
        {
            var profileJson = await sharedProfileSettingsService.GetAsync(
                profileId, UserSettingKeys.VideoPlaybackPolicy, cancellationToken);
            if (profileJson is not null)
                return JsonSerializer.Deserialize<VideoPlaybackPolicySettingsDto>(profileJson)
                    ?? new VideoPlaybackPolicySettingsDto();

            return await GetServerVideoPolicyAsync(cancellationToken);
        }

        if (userId is { } id)
        {
            var userJson = await userSettingsService.GetAsync(id, UserSettingKeys.VideoPlaybackPolicy, cancellationToken);
            if (userJson is not null)
                return JsonSerializer.Deserialize<VideoPlaybackPolicySettingsDto>(userJson) ?? new VideoPlaybackPolicySettingsDto();
        }

        return await GetServerVideoPolicyAsync(cancellationToken);
    }

    public async Task<AudioPlaybackPolicySettingsDto> GetEffectiveAudioPolicyAsync(
        Guid? userId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken = default)
    {
        if (sharedProfileId is { } profileId)
        {
            var profileJson = await sharedProfileSettingsService.GetAsync(
                profileId, UserSettingKeys.AudioPlaybackPolicy, cancellationToken);
            if (profileJson is not null)
                return JsonSerializer.Deserialize<AudioPlaybackPolicySettingsDto>(profileJson)
                    ?? new AudioPlaybackPolicySettingsDto();

            return await GetServerAudioPolicyAsync(cancellationToken);
        }

        if (userId is { } id)
        {
            var userJson = await userSettingsService.GetAsync(id, UserSettingKeys.AudioPlaybackPolicy, cancellationToken);
            if (userJson is not null)
                return JsonSerializer.Deserialize<AudioPlaybackPolicySettingsDto>(userJson) ?? new AudioPlaybackPolicySettingsDto();
        }

        return await GetServerAudioPolicyAsync(cancellationToken);
    }

    private async Task<VideoPlaybackPolicySettingsDto> GetServerVideoPolicyAsync(CancellationToken cancellationToken)
    {
        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.VideoPlaybackPolicy, cancellationToken);
        if (serverJson is not null)
            return JsonSerializer.Deserialize<VideoPlaybackPolicySettingsDto>(serverJson) ?? new VideoPlaybackPolicySettingsDto();

        return new VideoPlaybackPolicySettingsDto();
    }

    private async Task<AudioPlaybackPolicySettingsDto> GetServerAudioPolicyAsync(CancellationToken cancellationToken)
    {
        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.AudioPlaybackPolicy, cancellationToken);
        if (serverJson is not null)
            return JsonSerializer.Deserialize<AudioPlaybackPolicySettingsDto>(serverJson) ?? new AudioPlaybackPolicySettingsDto();

        return new AudioPlaybackPolicySettingsDto();
    }
}
