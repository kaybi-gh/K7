using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Libraries.Commands.UploadLibraryCover;

[Authorize(Roles = Roles.Administrator)]
public record UploadLibraryCoverCommand : IRequest<Guid>
{
    public required Guid LibraryGroupId { get; init; }

    // Mode 1: upload a new file
    public Stream? FileStream { get; init; }
    public string? FileName { get; init; }

    // Mode 2: pick an existing MetadataPicture from within the library
    public Guid? SourcePictureId { get; init; }
}

public class UploadLibraryCoverCommandHandler(
    IApplicationDbContext context,
    ICoverPictureUploadService coverUpload) : IRequestHandler<UploadLibraryCoverCommand, Guid>
{
    private const string CoverFolder = "library-groups";

    public async Task<Guid> Handle(UploadLibraryCoverCommand request, CancellationToken cancellationToken)
    {
        var libraryGroup = await context.LibraryGroups
            .Include(g => g.CoverPicture)
                .ThenInclude(p => p!.Variants)
            .FirstOrDefaultAsync(g => g.Id == request.LibraryGroupId, cancellationToken);

        Guard.Against.NotFound(request.LibraryGroupId, libraryGroup);

        if (libraryGroup.CoverPicture is not null)
            coverUpload.RemoveExistingCover(libraryGroup.CoverPicture, CoverFolder, request.LibraryGroupId);

        var localPath = await ResolveLocalPathAsync(request, cancellationToken);

        var picture = new MetadataPicture
        {
            Id = Guid.NewGuid(),
            Type = MetadataPictureType.Cover,
            LibraryGroupId = request.LibraryGroupId,
            LocalPath = localPath
        };

        context.MetadataPictures.Add(picture);
        await context.SaveChangesAsync(cancellationToken);

        await coverUpload.EnqueueVariantGenerationAsync(picture.Id, cancellationToken);

        return picture.Id;
    }

    private async Task<string> ResolveLocalPathAsync(
        UploadLibraryCoverCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FileStream is not null && request.FileName is not null)
        {
            return await coverUpload.SaveUploadedCoverAsync(
                request.FileStream,
                request.FileName,
                CoverFolder,
                request.LibraryGroupId,
                cancellationToken);
        }

        if (request.SourcePictureId is not null)
        {
            return await coverUpload.CopySourcePictureAsCoverAsync(
                request.SourcePictureId.Value,
                CoverFolder,
                request.LibraryGroupId,
                authorizeAsync: null,
                cancellationToken);
        }

        throw new ArgumentException("Either FileStream or SourcePictureId must be provided.");
    }
}
