using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Collections.Commands.UploadCollectionCover;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UploadCollectionCoverCommand : IRequest<Guid>
{
    public required Guid CollectionId { get; init; }
    public Stream? FileStream { get; init; }
    public string? FileName { get; init; }
    public Guid? SourcePictureId { get; init; }
}

public class UploadCollectionCoverCommandHandler(
    IApplicationDbContext context,
    ICoverPictureUploadService coverUpload,
    IUser currentUser) : IRequestHandler<UploadCollectionCoverCommand, Guid>
{
    public async Task<Guid> Handle(UploadCollectionCoverCommand request, CancellationToken cancellationToken)
    {
        var collection = await context.Collections
            .Include(c => c.CoverPicture)
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId && c.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.CollectionId, collection);

        if (collection.CoverPicture is not null)
        {
            context.MetadataPictures.Remove(collection.CoverPicture);
        }

        var localPath = await ResolveLocalPathAsync(request, collection.Id, cancellationToken);

        var picture = new MetadataPicture
        {
            Id = Guid.NewGuid(),
            Type = MetadataPictureType.Cover,
            CollectionId = collection.Id,
            LocalPath = localPath
        };

        context.MetadataPictures.Add(picture);
        await context.SaveChangesAsync(cancellationToken);

        if (request.FileStream is not null)
        {
            await coverUpload.EnqueueVariantGenerationAsync(picture.Id, cancellationToken);
        }

        return picture.Id;
    }

    private async Task<string> ResolveLocalPathAsync(
        UploadCollectionCoverCommand request,
        Guid collectionId,
        CancellationToken cancellationToken)
    {
        if (request.FileStream is not null && request.FileName is not null)
        {
            return await coverUpload.SaveUploadedCoverAsync(
                request.FileStream,
                request.FileName,
                "collections",
                collectionId,
                cancellationToken);
        }

        if (request.SourcePictureId is not null)
        {
            return await coverUpload.ResolveSourcePicturePathAsync(
                request.SourcePictureId.Value,
                async (source, ct) =>
                {
                    var mediaInCollection = await context.CollectionItems
                        .AnyAsync(i => i.CollectionId == collectionId && i.MediaId == source.MediaId, ct);

                    if (!mediaInCollection)
                    {
                        throw new ForbiddenAccessException();
                    }
                },
                cancellationToken);
        }

        throw new ArgumentException("Either FileStream or SourcePictureId must be provided.");
    }
}
