using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Json;

namespace K7.Server.Application.Features.Federation.Services;

public interface IUserFederationPrivacyService
{
    Task<FederationPrivacySettingsDto> GetPrivacyAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetPrivacyAsync(Guid userId, FederationPrivacySettingsDto settings, CancellationToken cancellationToken = default);
    Task<ReviewPreferencesDto> GetReviewPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetReviewPreferencesAsync(Guid userId, ReviewPreferencesDto preferences, CancellationToken cancellationToken = default);
}

public class UserFederationPrivacyService(
    IUserSettingsService userSettings,
    IVisibilityGrantService visibilityGrantService,
    IApplicationDbContext context) : IUserFederationPrivacyService
{
    public async Task<FederationPrivacySettingsDto> GetPrivacyAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var json = await userSettings.GetAsync(userId, UserSettingKeys.FederationPrivacy, cancellationToken);
        var settings = string.IsNullOrWhiteSpace(json)
            ? new FederationPrivacySettingsDto()
            : JsonSerializer.Deserialize<FederationPrivacySettingsDto>(json, K7JsonSerializerOptions.CreateDefault())
                ?? new FederationPrivacySettingsDto();

        var shareGrants = await visibilityGrantService.GetGlobalShareGrantsAsync(userId, cancellationToken);
        settings.Share.Grants = shareGrants.ToList();
        return settings;
    }

    public async Task SetPrivacyAsync(Guid userId, FederationPrivacySettingsDto settings, CancellationToken cancellationToken = default)
    {
        await ValidateShareGrantsAsync(settings.Share.Grants, cancellationToken);

        var json = JsonSerializer.Serialize(settings, K7JsonSerializerOptions.CreateDefault());
        await userSettings.SetAsync(userId, UserSettingKeys.FederationPrivacy, json, cancellationToken);
        await visibilityGrantService.SetGlobalShareGrantsAsync(userId, settings.Share.Grants, cancellationToken);
    }

    public async Task<ReviewPreferencesDto> GetReviewPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var json = await userSettings.GetAsync(userId, UserSettingKeys.ReviewPreferences, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return new ReviewPreferencesDto();

        return JsonSerializer.Deserialize<ReviewPreferencesDto>(json, K7JsonSerializerOptions.CreateDefault())
            ?? new ReviewPreferencesDto();
    }

    public async Task SetReviewPreferencesAsync(Guid userId, ReviewPreferencesDto preferences, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(preferences, K7JsonSerializerOptions.CreateDefault());
        await userSettings.SetAsync(userId, UserSettingKeys.ReviewPreferences, json, cancellationToken);
    }

    private async Task ValidateShareGrantsAsync(
        IReadOnlyList<FederationVisibilityGrantDto> grants,
        CancellationToken cancellationToken)
    {
        if (grants.Count == 0)
            return;

        var localUserIds = grants.Where(g => g.TargetUserId is not null).Select(g => g.TargetUserId!.Value).ToHashSet();
        if (localUserIds.Count > 0)
        {
            var validLocalCount = await context.Users
                .CountAsync(u => localUserIds.Contains(u.Id) && u.PeerServerId == null && u.IsActive, cancellationToken);
            if (validLocalCount != localUserIds.Count)
                throw new Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("Grants", "One or more grant targets are invalid.")]);
        }

        var peerIds = grants.Where(g => g.TargetPeerServerId is not null).Select(g => g.TargetPeerServerId!.Value).ToHashSet();
        if (peerIds.Count > 0)
        {
            var validPeerCount = await context.PeerServers
                .CountAsync(p => peerIds.Contains(p.Id) && p.Status == Domain.Enums.PeerStatus.Active, cancellationToken);
            if (validPeerCount != peerIds.Count)
                throw new Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("Grants", "One or more federation grant targets are not direct peers.")]);
        }
    }
}
