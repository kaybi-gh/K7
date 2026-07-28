using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.SharedProfiles.Commands.UploadSharedProfileAvatar;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UploadSharedProfileAvatarCommand : IRequest
{
    public required Guid SharedProfileId { get; init; }
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
}

public class UploadSharedProfileAvatarCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService,
    ISender sender,
    IOptions<PathsConfiguration> pathsConfiguration,
    ILogger<UploadSharedProfileAvatarCommandHandler> logger)
    : IRequestHandler<UploadSharedProfileAvatarCommand>
{
    public async Task Handle(UploadSharedProfileAvatarCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await SharedProfileMemberValidator.GetGroupForHostAsync(
            context, identityService, request.SharedProfileId, userId, currentUser.IdentityId, cancellationToken);

        var existingPicture = await context.MetadataPictures
            .FirstOrDefaultAsync(
                p => p.SharedProfileId == request.SharedProfileId
                     && p.Type == MetadataPictureType.SharedProfileAvatar,
                cancellationToken);

        if (existingPicture is not null)
        {
            if (existingPicture.LocalPath is not null && File.Exists(existingPicture.LocalPath))
                File.Delete(existingPicture.LocalPath);

            var existingVariants = await context.MetadataPictureVariants
                .Where(v => v.MetadataPictureId == existingPicture.Id)
                .ToListAsync(cancellationToken);

            foreach (var variant in existingVariants)
            {
                if (variant.LocalPath is not null && File.Exists(variant.LocalPath))
                    File.Delete(variant.LocalPath);
            }

            context.MetadataPictureVariants.RemoveRange(existingVariants);
            context.MetadataPictures.Remove(existingPicture);
        }

        var ext = Path.GetExtension(request.FileName);
        var pictureId = Guid.NewGuid();
        var directory = Path.Combine(pathsConfiguration.Value.Metadatas, "shared-profiles", $"{request.SharedProfileId}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"{pictureId}{ext}");

        await using (var fs = File.Create(filePath))
        {
            await request.FileStream.CopyToAsync(fs, cancellationToken);
        }

        logger.LogInformation(
            "Saved avatar for shared profile {SharedProfileId} to {Path}",
            request.SharedProfileId,
            filePath);

        var picture = new MetadataPicture
        {
            Id = pictureId,
            Type = MetadataPictureType.SharedProfileAvatar,
            SharedProfileId = request.SharedProfileId,
            LocalPath = filePath
        };

        context.MetadataPictures.Add(picture);
        await context.SaveChangesAsync(cancellationToken);

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new GenerateMetadataPictureVariantsCommand { MetadataPictureId = picture.Id },
            TargetEntityId = picture.Id,
            TargetEntityTypeName = nameof(MetadataPicture),
            Lane = BackgroundTaskLane.ImageProcessing,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.User
        }, cancellationToken);
    }
}
