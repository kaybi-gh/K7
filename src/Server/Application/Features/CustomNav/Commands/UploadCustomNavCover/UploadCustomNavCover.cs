using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.CustomNav.Commands.UploadCustomNavCover;

[Authorize]
public record UploadCustomNavCoverCommand : IRequest<Guid>
{
    public required Guid ItemId { get; init; }
    public Stream? FileStream { get; init; }
    public string? FileName { get; init; }
    public Guid? SourcePictureId { get; init; }
    public Guid? ReplacePictureId { get; init; }
}

public class UploadCustomNavCoverCommandHandler(
    IApplicationDbContext context,
    ICoverPictureUploadService coverUpload,
    IUser currentUser) : IRequestHandler<UploadCustomNavCoverCommand, Guid>
{
    private const string CoverFolder = "custom-nav";

    public async Task<Guid> Handle(UploadCustomNavCoverCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);

        if (request.ReplacePictureId is { } replaceId)
        {
            var existing = await context.MetadataPictures
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(
                    p => p.Id == replaceId && p.UserId == userId && p.Type == MetadataPictureType.Cover,
                    cancellationToken);

            if (existing is not null)
                coverUpload.RemoveExistingCover(existing, CoverFolder, request.ItemId);
        }

        var localPath = await ResolveLocalPathAsync(request, cancellationToken);

        var picture = new MetadataPicture
        {
            Id = Guid.NewGuid(),
            Type = MetadataPictureType.Cover,
            UserId = userId,
            LocalPath = localPath
        };

        context.MetadataPictures.Add(picture);
        await context.SaveChangesAsync(cancellationToken);

        await coverUpload.EnqueueVariantGenerationAsync(picture.Id, cancellationToken);

        return picture.Id;
    }

    private async Task<string> ResolveLocalPathAsync(
        UploadCustomNavCoverCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FileStream is not null && request.FileName is not null)
        {
            return await coverUpload.SaveUploadedCoverAsync(
                request.FileStream,
                request.FileName,
                CoverFolder,
                request.ItemId,
                cancellationToken);
        }

        if (request.SourcePictureId is not null)
        {
            return await coverUpload.CopySourcePictureAsCoverAsync(
                request.SourcePictureId.Value,
                CoverFolder,
                request.ItemId,
                authorizeAsync: null,
                cancellationToken);
        }

        throw new ArgumentException("Either FileStream or SourcePictureId must be provided.");
    }
}
