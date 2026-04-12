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

public class BulkUpsertRatingsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<BulkUpsertRatingsCommand, int>
{
    public async Task<int> Handle(BulkUpsertRatingsCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        Guard.Against.NotFound(request.UserId, user);

        var mediaIds = request.Items.Select(i => i.MediaId).Distinct().ToList();

        var existingRatings = await context.Ratings
            .OfType<UserRating>()
            .Where(r => r.UserId == request.UserId && mediaIds.Contains(r.MediaId))
            .ToDictionaryAsync(r => r.MediaId, cancellationToken);

        var upsertedCount = 0;
        var strategy = request.Strategy ?? new MergeStrategy();

        foreach (var item in request.Items)
        {
            if (existingRatings.TryGetValue(item.MediaId, out var existing))
            {
                if (strategy.Rating is RatingConflictMode.Overwrite)
                {
                    existing.Value = item.Value;
                }
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
            }

            upsertedCount++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return upsertedCount;
    }
}
