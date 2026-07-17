using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfilePlaylistIds;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetSharedProfilePlaylistIdsQuery(Guid SharedProfileId) : IRequest<IReadOnlyList<Guid>>;

public class GetSharedProfilePlaylistIdsQueryHandler(
    IApplicationDbContext context,
    IUser currentUser)
    : IRequestHandler<GetSharedProfilePlaylistIdsQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetSharedProfilePlaylistIdsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await SharedProfileMemberValidator.GetGroupForMemberAsync(
            context, request.SharedProfileId, userId, cancellationToken);

        return await context.SharedProfilePlaylists
            .AsNoTracking()
            .Where(sp => sp.SharedProfileId == request.SharedProfileId)
            .Select(sp => sp.PlaylistId)
            .ToListAsync(cancellationToken);
    }
}
