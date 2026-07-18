using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    ISender sender,
    IOptions<PathsConfiguration> pathsConfiguration,
    IUser currentUser,
    ILogger<UploadCollectionCoverCommandHandler> logger)
    : IRequestHandler<UploadCollectionCoverCommand, Guid>
{
    private readonly PathsConfiguration _pathsConfiguration = pathsConfiguration.Value;

    public async Task<Guid> Handle(UploadCollectionCoverCommand request, CancellationToken cancellationToken)
    {
        var collection = await context.Collections
            .Include(c => c.CoverPicture)
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId && c.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.CollectionId, collection);

        if (collection.CoverPicture is not null)
            context.MetadataPictures.Remove(collection.CoverPicture);

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
            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new GenerateMetadataPictureVariantsCommand { MetadataPictureId = picture.Id },
                Priority = BackgroundTaskPriority.Normal,
                TargetEntityId = picture.Id,
                TargetEntityTypeName = nameof(MetadataPicture)
            }, cancellationToken);
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
            var ext = Path.GetExtension(request.FileName);
            var directory = Path.Combine(_pathsConfiguration.Metadatas, "collections", $"{collectionId}");
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, $"cover{ext}");

            await using (var fs = File.Create(filePath))
                await request.FileStream.CopyToAsync(fs, cancellationToken);

            logger.LogInformation("Saved uploaded collection cover for {CollectionId} to {Path}", collectionId, filePath);
            return _pathsConfiguration.ToRelativeMetadataPath(filePath);
        }

        if (request.SourcePictureId is not null)
        {
            var source = await context.MetadataPictures
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.SourcePictureId, cancellationToken);

            Guard.Against.NotFound(request.SourcePictureId.Value, source);

            var mediaInCollection = await context.CollectionItems
                .AnyAsync(i => i.CollectionId == collectionId && i.MediaId == source.MediaId, cancellationToken);

            if (!mediaInCollection)
                throw new ForbiddenAccessException();

            return source.LocalPath ?? string.Empty;
        }

        throw new ArgumentException("Either FileStream or SourcePictureId must be provided.");
    }
}
