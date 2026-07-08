using K7.Shared.Constants;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Helpers;

public static class UserPreferenceOverrideHelper
{
    public static Task<bool> HasHomeLayoutOverrideAsync(
        IUserPreferencesService service,
        CancellationToken cancellationToken = default) =>
        service.UserSettingExistsAsync(UserPreferenceKeys.HomeLayout, cancellationToken);

    public static async Task<bool> HasAudioOverridesAsync(
        IUserPreferencesService service,
        CancellationToken cancellationToken = default) =>
        await service.UserSettingExistsAsync(UserPreferenceKeys.AudioPlayerSettings, cancellationToken)
        || await service.UserSettingExistsAsync(UserPreferenceKeys.AudioPlaybackPolicy, cancellationToken);

    public static async Task<bool> HasVideoOverridesAsync(
        IUserPreferencesService service,
        Guid? libraryId,
        CancellationToken cancellationToken = default)
    {
        var trackKey = libraryId is { } id
            ? UserPreferenceKeys.TrackSelectionForLibrary(id)
            : UserPreferenceKeys.TrackSelectionPreferences;

        return await service.UserSettingExistsAsync(UserPreferenceKeys.VideoPlayerSettings, cancellationToken)
            || await service.UserSettingExistsAsync(UserPreferenceKeys.VideoPlaybackPolicy, cancellationToken)
            || await service.UserSettingExistsAsync(trackKey, cancellationToken);
    }
}

public static class ServerPreferenceOverrideHelper
{
    public static async Task<bool> HasHomeLayoutOverrideAsync(
        IServerPreferencesService service,
        CancellationToken cancellationToken = default) =>
        await service.GetServerHomeLayoutAsync(cancellationToken) is not null;

    public static async Task<bool> HasAudioOverridesAsync(
        IServerPreferencesService service,
        CancellationToken cancellationToken = default) =>
        await service.GetServerAudioPlayerSettingsAsync(cancellationToken) is not null
        || await service.GetServerAudioPlaybackPolicySettingsAsync(cancellationToken) is not null;

    public static async Task<bool> HasVideoOverridesAsync(
        IServerPreferencesService service,
        Guid? libraryId,
        CancellationToken cancellationToken = default) =>
        await service.GetServerVideoPlayerSettingsAsync(cancellationToken) is not null
        || await service.GetServerVideoPlaybackPolicySettingsAsync(cancellationToken) is not null
        || await service.GetServerTrackSelectionPreferencesAsync(libraryId, cancellationToken) is not null;
}
