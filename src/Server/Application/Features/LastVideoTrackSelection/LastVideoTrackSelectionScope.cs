using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Settings;
using K7.Shared.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.LastVideoTrackSelection;

public static class LastVideoTrackSelectionScope
{
    public static async Task<SettingKey<string>?> ResolveSettingKeyAsync(
        IApplicationDbContext context,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var media = await context.Medias
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        if (media is null)
            return null;

        return media switch
        {
            Movie => new SettingKey<string>(UserPreferenceKeys.LastVideoTrackSelectionForMovie(media.Id)),
            Serie => new SettingKey<string>(UserPreferenceKeys.LastVideoTrackSelectionForSerie(media.Id)),
            SerieEpisode episode => new SettingKey<string>(
                UserPreferenceKeys.LastVideoTrackSelectionForSerie(episode.SerieId)),
            _ => null
        };
    }

    public static async Task<string?> ResolveOriginalLanguageAsync(
        IApplicationDbContext context,
        Guid? mediaId,
        CancellationToken cancellationToken = default)
    {
        if (mediaId is null)
            return null;

        var media = await context.Medias
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mediaId.Value, cancellationToken);

        return media switch
        {
            Movie movie => movie.OriginalLanguage,
            Serie serie => serie.OriginalLanguage,
            SerieEpisode episode => await context.Medias
                .OfType<Serie>()
                .AsNoTracking()
                .Where(s => s.Id == episode.SerieId)
                .Select(s => s.OriginalLanguage)
                .FirstOrDefaultAsync(cancellationToken),
            _ => null
        };
    }

    /// <summary>
    /// Personal RememberTrackSelection, or the shared-profile host's when a hat is active.
    /// </summary>
    public static async Task<bool> IsRememberEnabledAsync(
        IApplicationDbContext context,
        IUserSettingsService userSettingsService,
        IServerSettingsService serverSettingsService,
        IUser currentUser,
        CancellationToken cancellationToken = default)
    {
        Guid? settingsUserId = currentUser.Id;
        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        if (sharedProfileId is { } profileId)
        {
            settingsUserId = await context.SharedProfiles
                .AsNoTracking()
                .Where(p => p.Id == profileId)
                .Select(p => (Guid?)p.HostUserId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (settingsUserId is { } userId)
        {
            var userJson = await userSettingsService.GetAsync(
                userId, UserSettingKeys.VideoPlayerSettings, cancellationToken);
            if (userJson is not null)
            {
                var userSettings = JsonSerializer.Deserialize<VideoPlayerSettingsDto>(userJson);
                return userSettings?.RememberTrackSelection != false;
            }
        }

        var serverJson = await serverSettingsService.GetAsync(
            ServerSettingKeys.VideoPlayerSettings, cancellationToken);
        if (serverJson is not null)
        {
            var serverSettings = JsonSerializer.Deserialize<VideoPlayerSettingsDto>(serverJson);
            return serverSettings?.RememberTrackSelection != false;
        }

        return true;
    }
}
