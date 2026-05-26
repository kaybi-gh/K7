using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Users.Commands.RemoveUserAvatar;

public record RemoveUserAvatarCommand : IRequest;

public class RemoveUserAvatarCommandHandler(
    IApplicationDbContext context,
    IUser currentUser) : IRequestHandler<RemoveUserAvatarCommand>
{
    public async Task Handle(RemoveUserAvatarCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);

        var picture = await context.MetadataPictures
            .FirstOrDefaultAsync(p => p.UserId == currentUser.Id && p.Type == MetadataPictureType.UserAvatar, cancellationToken);

        if (picture is null)
            return;

        if (picture.LocalPath is not null && File.Exists(picture.LocalPath))
            File.Delete(picture.LocalPath);

        // Remove variants
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
