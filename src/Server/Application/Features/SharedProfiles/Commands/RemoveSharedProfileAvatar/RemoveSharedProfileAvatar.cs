using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.SharedProfiles.Commands.RemoveSharedProfileAvatar;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record RemoveSharedProfileAvatarCommand(Guid SharedProfileId) : IRequest;

public class RemoveSharedProfileAvatarCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService)
    : IRequestHandler<RemoveSharedProfileAvatarCommand>
{
    public async Task Handle(RemoveSharedProfileAvatarCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await SharedProfileMemberValidator.GetGroupForHostAsync(
            context, identityService, request.SharedProfileId, userId, currentUser.IdentityId, cancellationToken);

        var picture = await context.MetadataPictures
            .FirstOrDefaultAsync(
                p => p.SharedProfileId == request.SharedProfileId
                     && p.Type == MetadataPictureType.SharedProfileAvatar,
                cancellationToken);

        if (picture is null)
            return;

        if (picture.LocalPath is not null && File.Exists(picture.LocalPath))
            File.Delete(picture.LocalPath);

        var variants = await context.MetadataPictureVariants
            .Where(v => v.MetadataPictureId == picture.Id)
            .ToListAsync(cancellationToken);

        foreach (var variant in variants)
        {
            if (variant.LocalPath is not null && File.Exists(variant.LocalPath))
                File.Delete(variant.LocalPath);
        }

        context.MetadataPictureVariants.RemoveRange(variants);
        context.MetadataPictures.Remove(picture);
        await context.SaveChangesAsync(cancellationToken);
    }
}
