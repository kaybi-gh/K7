using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Ratings;
using K7.Shared.Dtos.Requests;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Users.Commands.BulkUpsertRatings;

[Authorize(Roles = Roles.Administrator)]
public record BulkUpsertRatingsCommand : IRequest<int>
{
    public required Guid UserId { get; init; }
    public required IReadOnlyList<BulkUpsertRatingsRequest.RatingItem> Items { get; init; }
    public MergeStrategy? Strategy { get; init; }
}

public class BulkUpsertRatingsCommandHandler(
    IApplicationDbContext context,
    IMediaQueryCacheInvalidator cacheInvalidator)
    : IRequestHandler<BulkUpsertRatingsCommand, int>
{
    public async Task<int> Handle(BulkUpsertRatingsCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        Guard.Against.NotFound(request.UserId, user);

        var mediaIds = request.Items.Select(i => i.MediaId).Distinct().ToList();

        // One rating per (user, media). GroupBy is a safety net if MakeUserRatingMediaUserUnique
        // has not been applied yet; BulkUpsertRatings also tracks in-batch inserts.
        var existingRatings = (await context.Ratings
                .OfType<UserRating>()
                .Where(r => r.UserId == request.UserId && mediaIds.Contains(r.MediaId))
                .ToListAsync(cancellationToken))
            .GroupBy(r => r.MediaId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.LastModified).ThenByDescending(r => r.Id).First());

        var upsertedCount = 0;
        var strategy = request.Strategy ?? new MergeStrategy();

        foreach (var item in request.Items)
        {
            if (existingRatings.TryGetValue(item.MediaId, out var existing))
            {
                if (strategy.Rating is not RatingConflictMode.Overwrite)
                    continue;

                existing.Value = item.Value;
            }
            else
            {
                var rating = new UserRating
                {
                    UserId = request.UserId,
                    MediaId = item.MediaId,
                    Value = item.Value,
                    MinimumValue = 0,
                    MaximumValue = 10
                };
                context.Ratings.Add(rating);
                // Same pattern as BulkUpsertMediaStates: track inserts so duplicate MediaIds in
                // one request (common after Plex match collapse) do not create extra rows.
                existingRatings[item.MediaId] = rating;
            }

            upsertedCount++;
        }

        await context.SaveChangesAsync(cancellationToken);
        if (upsertedCount > 0)
            cacheInvalidator.InvalidateAll();
        return upsertedCount;
    }
}
