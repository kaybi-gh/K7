using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Commands.RateMedia;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record RateMediaCommand(Guid MediaId, int Value) : IRequest;

public class RateMediaCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMediaAccessGuard accessGuard,
    IMediaQueryCacheInvalidator cacheInvalidator,
    IUserRatingNotifier ratingNotifier,
    IIdentityService identityService)
    : IRequestHandler<RateMediaCommand>
{
    public async Task Handle(RateMediaCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return;

        await accessGuard.EnsureAccessAsync(request.MediaId, cancellationToken);

        var rating = await context.Ratings
            .OfType<UserRating>()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.MediaId == request.MediaId, cancellationToken);

        var isNew = rating is null;
        if (rating is null)
        {
            rating = new UserRating
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MediaId = request.MediaId,
                Value = request.Value,
                MinimumValue = 0,
                MaximumValue = 10
            };
            context.Ratings.Add(rating);
        }
        else
        {
            rating.Value = request.Value;
        }

        var userName = currentUser.IdentityId is not null
            ? await identityService.GetUserNameAsync(currentUser.IdentityId)
            : null;

        rating.AddDomainEvent(new MediaRatedEvent(
            userId,
            userName,
            request.MediaId,
            request.Value,
            isNew));

        await context.SaveChangesAsync(cancellationToken);
        cacheInvalidator.InvalidateAll();

        if (currentUser.IdentityId is { } identityId)
        {
            await ratingNotifier.NotifyUserRatingUpdatedAsync(
                identityId,
                request.MediaId,
                request.Value,
                cancellationToken);
        }
    }
}
