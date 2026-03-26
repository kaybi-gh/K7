using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Ratings;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Commands.RateMedia;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record RateMediaCommand(Guid MediaId, int Value) : IRequest;

public class RateMediaCommandHandler(IApplicationDbContext context, IUser currentUser, IMediaAccessGuard accessGuard)
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

        if (rating is null)
        {
            rating = new UserRating
            {
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

        await context.SaveChangesAsync(cancellationToken);
    }
}
