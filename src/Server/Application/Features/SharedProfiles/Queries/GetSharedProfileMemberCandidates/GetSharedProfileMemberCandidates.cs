using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.SharedProfiles;

namespace K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfileMemberCandidates;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetSharedProfileMemberCandidatesQuery : IRequest<IReadOnlyList<SharedProfileMemberCandidateDto>>;

public class GetSharedProfileMemberCandidatesQueryHandler(
    IApplicationDbContext context,
    IIdentityService identityService)
    : IRequestHandler<GetSharedProfileMemberCandidatesQuery, IReadOnlyList<SharedProfileMemberCandidateDto>>
{
    public async Task<IReadOnlyList<SharedProfileMemberCandidateDto>> Handle(
        GetSharedProfileMemberCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        var users = await context.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.PeerServerId == null && u.IdentityUserId != null)
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName, u.IdentityUserId })
            .ToListAsync(cancellationToken);

        var candidates = new List<(Guid Id, string DisplayName)>();

        foreach (var user in users)
        {
            if (await identityService.IsInRoleAsync(user.IdentityUserId!, Roles.Guest))
                continue;

            var displayName = user.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = await identityService.GetUserNameAsync(user.IdentityUserId!);

            if (string.IsNullOrWhiteSpace(displayName))
                continue;

            candidates.Add((user.Id, displayName.Trim()));
        }

        var userIds = candidates.Select(u => u.Id).ToList();
        var blocked = await SharedProfilePreferencesHelper.GetUsersBlockingMembershipAsync(context, userIds, cancellationToken);

        var avatarMap = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.UserId != null && userIds.Contains(p.UserId.Value) && p.Type == MetadataPictureType.UserAvatar)
            .Select(p => new { p.UserId, p.Id })
            .ToDictionaryAsync(p => p.UserId!.Value, p => $"/api/metadata-pictures/{p.Id}", cancellationToken);

        return candidates
            .Where(u => !blocked.Contains(u.Id))
            .Select(u => new SharedProfileMemberCandidateDto
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                AvatarUrl = avatarMap.GetValueOrDefault(u.Id)
            })
            .ToList();
    }
}
