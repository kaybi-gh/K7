using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Users.Queries.GetGuardianSessionTarget;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.SharedProfiles;

namespace K7.Server.Application.Features.SharedProfiles.Queries.GetDeviceLinkSharedProfiles;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetDeviceLinkSharedProfilesQuery(Guid? TargetUserId) : IRequest<IReadOnlyList<DeviceLinkSharedProfileDto>>;

public class GetDeviceLinkSharedProfilesQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    ISender sender)
    : IRequestHandler<GetDeviceLinkSharedProfilesQuery, IReadOnlyList<DeviceLinkSharedProfileDto>>
{
    public async Task<IReadOnlyList<DeviceLinkSharedProfileDto>> Handle(
        GetDeviceLinkSharedProfilesQuery request,
        CancellationToken cancellationToken)
    {
        Guid domainUserId;
        if (request.TargetUserId is null)
        {
            if (await currentUser.GetIdAsync(cancellationToken) is not { } id)
                return [];

            domainUserId = id;
        }
        else
        {
            if (string.IsNullOrEmpty(currentUser.IdentityId))
                throw new ForbiddenAccessException();

            var target = await sender.Send(
                new GetGuardianSessionTargetQuery(currentUser.IdentityId, request.TargetUserId.Value),
                cancellationToken);
            domainUserId = target.UserId;
        }

        var groups = await context.SharedProfiles
            .AsNoTracking()
            .Where(group => group.HostUserId == domainUserId || group.Members.Any(member => member.UserId == domainUserId))
            .OrderBy(group => group.Name)
            .Select(group => new { group.Id, group.Name, HasPin = group.PinHash != null })
            .ToListAsync(cancellationToken);

        if (groups.Count == 0)
            return [];

        var ids = groups.Select(group => group.Id).ToList();
        var avatars = await context.MetadataPictures
            .AsNoTracking()
            .Where(picture => picture.SharedProfileId != null
                              && ids.Contains(picture.SharedProfileId.Value)
                              && picture.Type == MetadataPictureType.SharedProfileAvatar)
            .Select(picture => new { picture.SharedProfileId, picture.Id })
            .ToDictionaryAsync(
                picture => picture.SharedProfileId!.Value,
                picture => $"/api/metadata-pictures/{picture.Id}",
                cancellationToken);

        return groups
            .Select(group => new DeviceLinkSharedProfileDto
            {
                Id = group.Id,
                Name = group.Name,
                HasPin = group.HasPin,
                AvatarUrl = avatars.GetValueOrDefault(group.Id)
            })
            .ToList();
    }
}
