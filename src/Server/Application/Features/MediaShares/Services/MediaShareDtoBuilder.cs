using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Shares;
using K7.Shared.Dtos.Shares;

namespace K7.Server.Application.Features.MediaShares.Services;

/// <summary>
/// Builds the management view of shares: media description, live status and creator name.
/// </summary>
public static class MediaShareDtoBuilder
{
    public static async Task<List<MediaShareDto>> BuildAsync(
        IApplicationDbContext context,
        IIdentityService identityService,
        IMediaShareManager shareManager,
        IReadOnlyList<MediaShare> shares,
        CancellationToken cancellationToken = default)
    {
        if (shares.Count == 0)
            return [];

        var descriptions = await ShareMediaDescriber.DescribeAsync(
            context,
            shares.Select(s => s.RootMediaId).Distinct().ToList(),
            cancellationToken);

        var creatorIds = shares.Select(s => s.CreatedByUserId).Distinct().ToList();
        var creators = await context.Users
            .AsNoTracking()
            .Where(u => creatorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.IdentityUserId })
            .ToListAsync(cancellationToken);

        var userNames = await identityService.GetUserNamesAsync(
            creators.Where(c => c.IdentityUserId is not null).Select(c => c.IdentityUserId!));

        var creatorNames = creators.ToDictionary(
            c => c.Id,
            c => !string.IsNullOrWhiteSpace(c.DisplayName)
                ? c.DisplayName
                : c.IdentityUserId is not null ? userNames.GetValueOrDefault(c.IdentityUserId) : null);

        var now = DateTimeOffset.UtcNow;
        var result = new List<MediaShareDto>(shares.Count);
        foreach (var share in shares)
        {
            var canShare = await shareManager.CanUserShareAsync(share.CreatedByUserId, cancellationToken);
            descriptions.TryGetValue(share.RootMediaId, out var description);

            result.Add(new MediaShareDto
            {
                Id = share.Id,
                RootMediaId = share.RootMediaId,
                MediaType = description?.Type ?? default,
                MediaTitle = description?.Title ?? string.Empty,
                MediaSubtitle = description?.Subtitle,
                PictureUri = description?.PictureUri,
                Label = share.Label,
                HasPassword = share.HasPassword,
                ExpiresAt = share.ExpiresAt,
                MaxAccessCount = share.MaxAccessCount,
                AccessCount = share.AccessCount,
                AllowPlayback = share.AllowPlayback,
                AllowDownload = share.AllowDownload,
                CloseWhenConsumed = share.CloseWhenConsumed,
                Status = MediaShareManager.ResolveStatus(share, canShare, now),
                CreatedAt = share.Created,
                ConsumedAt = share.ConsumedAt,
                RevokedAt = share.RevokedAt,
                LastAccessedAt = share.LastAccessedAt,
                CreatedByUserId = share.CreatedByUserId,
                CreatedByName = creatorNames.GetValueOrDefault(share.CreatedByUserId)
            });
        }

        return result;
    }
}
