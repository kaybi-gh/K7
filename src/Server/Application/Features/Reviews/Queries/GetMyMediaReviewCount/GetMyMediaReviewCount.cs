using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Reviews.Queries.GetMyMediaReviewCount;

[Authorize]
public record GetMyMediaReviewCountQuery : IRequest<int>;

public class GetMyMediaReviewCountQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetMyMediaReviewCountQuery, int>
{
    public async Task<int> Handle(GetMyMediaReviewCountQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return 0;

        return await context.MediaReviews
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId, cancellationToken);
    }
}
