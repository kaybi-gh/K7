using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.MediaShares.Services;

public static class ShareConsumption
{
    /// <summary>
    /// Closes the share when it is set to close once consumed and the consumed media is
    /// its root. Visitors already in keep access for a short grace period.
    /// </summary>
    public static async Task MarkConsumedAsync(
        IApplicationDbContext context,
        Guid shareId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await context.MediaShares
            .Where(s => s.Id == shareId
                && s.CloseWhenConsumed
                && s.ConsumedAt == null
                && s.RootMediaId == mediaId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.ConsumedAt, now), cancellationToken);
    }
}
