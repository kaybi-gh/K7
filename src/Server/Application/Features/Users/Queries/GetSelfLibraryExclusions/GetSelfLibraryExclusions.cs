using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Users.Queries.GetSelfLibraryExclusions;

public record GetSelfLibraryExclusionsQuery : IRequest<IReadOnlyList<Guid>>;

public class GetSelfLibraryExclusionsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetSelfLibraryExclusionsQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(GetSelfLibraryExclusionsQuery request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);

        return await context.UserLibraryExclusions
            .Where(e => e.UserId == userId && e.IsSelfExcluded)
            .Select(e => e.LibraryId)
            .ToListAsync(cancellationToken);
    }
}
