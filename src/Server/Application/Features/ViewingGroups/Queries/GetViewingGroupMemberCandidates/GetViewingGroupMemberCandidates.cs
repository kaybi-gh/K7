using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.ViewingGroups;

namespace K7.Server.Application.Features.ViewingGroups.Queries.GetViewingGroupMemberCandidates;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetViewingGroupMemberCandidatesQuery : IRequest<IReadOnlyList<ViewingGroupMemberCandidateDto>>;

public class GetViewingGroupMemberCandidatesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetViewingGroupMemberCandidatesQuery, IReadOnlyList<ViewingGroupMemberCandidateDto>>
{
    public async Task<IReadOnlyList<ViewingGroupMemberCandidateDto>> Handle(
        GetViewingGroupMemberCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        var users = await context.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.PeerServerId == null && u.IdentityUserId != null)
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();
        var avatarMap = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.UserId != null && userIds.Contains(p.UserId.Value) && p.Type == MetadataPictureType.UserAvatar)
            .Select(p => new { p.UserId, p.Id })
            .ToDictionaryAsync(p => p.UserId!.Value, p => $"/api/metadata-pictures/{p.Id}", cancellationToken);

        return users
            .Select(u => new ViewingGroupMemberCandidateDto
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                AvatarUrl = avatarMap.GetValueOrDefault(u.Id)
            })
            .ToList();
    }
}
