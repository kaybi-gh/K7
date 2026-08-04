using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.MetadataPictures.Services;

public interface ICoverPictureUploadService
{
    Task<string> SaveUploadedCoverAsync(
        Stream fileStream,
        string fileName,
        string folderName,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies an existing metadata picture file into the owner's cover folder so the cover
    /// remains available if the source media picture is later replaced or deleted.
    /// </summary>
    Task<string> CopySourcePictureAsCoverAsync(
        Guid sourcePictureId,
        string folderName,
        Guid ownerId,
        Func<MetadataPicture, CancellationToken, Task>? authorizeAsync = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a cover picture from the DB. Deletes files only when they live under the
    /// owner's cover folder (avoids deleting a media picture file shared by legacy covers).
    /// </summary>
    void RemoveExistingCover(MetadataPicture cover, string folderName, Guid ownerId);

    Task EnqueueVariantGenerationAsync(Guid metadataPictureId, CancellationToken cancellationToken = default);
}

public sealed class CoverPictureUploadService(
    IApplicationDbContext context,
    ISender sender,
    IOptions<PathsConfiguration> pathsConfiguration,
    ILogger<CoverPictureUploadService> logger) : ICoverPictureUploadService
{
    private readonly PathsConfiguration _paths = pathsConfiguration.Value;

    public async Task<string> SaveUploadedCoverAsync(
        Stream fileStream,
        string fileName,
        string folderName,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName);
        var filePath = PrepareCoverFilePath(folderName, ownerId, ext);

        await using (var fs = File.Create(filePath))
        {
            await fileStream.CopyToAsync(fs, cancellationToken);
        }

        logger.LogInformation("Saved uploaded cover for {OwnerId} under {Folder} to {Path}", ownerId, folderName, filePath);
        return _paths.ToRelativeMetadataPath(filePath);
    }

    public async Task<string> CopySourcePictureAsCoverAsync(
        Guid sourcePictureId,
        string folderName,
        Guid ownerId,
        Func<MetadataPicture, CancellationToken, Task>? authorizeAsync = null,
        CancellationToken cancellationToken = default)
    {
        var source = await context.MetadataPictures
            .AsNoTracking()
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == sourcePictureId, cancellationToken);

        Guard.Against.NotFound(sourcePictureId, source);

        if (authorizeAsync is not null)
            await authorizeAsync(source, cancellationToken);

        var sourcePath = ResolveReadableSourcePath(source);
        if (sourcePath is null)
        {
            throw new InvalidOperationException(
                $"Source picture {sourcePictureId} has no readable local file to copy as cover.");
        }

        var ext = Path.GetExtension(sourcePath);
        var filePath = PrepareCoverFilePath(folderName, ownerId, ext);

        await using (var sourceStream = File.OpenRead(sourcePath))
        await using (var destStream = File.Create(filePath))
        {
            await sourceStream.CopyToAsync(destStream, cancellationToken);
        }

        logger.LogInformation(
            "Copied source picture {SourcePictureId} as cover for {OwnerId} under {Folder} to {Path}",
            sourcePictureId,
            ownerId,
            folderName,
            filePath);

        return _paths.ToRelativeMetadataPath(filePath);
    }

    public void RemoveExistingCover(MetadataPicture cover, string folderName, Guid ownerId)
    {
        if (IsOwnedCoverPath(cover.LocalPath, folderName, ownerId))
            DeleteCoverFiles(cover);

        context.MetadataPictures.Remove(cover);
    }

    public Task EnqueueVariantGenerationAsync(Guid metadataPictureId, CancellationToken cancellationToken = default)
    {
        return sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new GenerateMetadataPictureVariantsCommand { MetadataPictureId = metadataPictureId },
            TargetEntityId = metadataPictureId,
            TargetEntityTypeName = nameof(MetadataPicture),
            Lane = BackgroundTaskLane.ImageProcessing,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System
        }, cancellationToken);
    }

    private string PrepareCoverFilePath(string folderName, Guid ownerId, string? extension)
    {
        var ext = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension;
        var directory = Path.Combine(_paths.Metadatas, folderName, $"{ownerId}");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"cover{ext}");
    }

    private static string? ResolveReadableSourcePath(MetadataPicture source)
    {
        if (source.LocalPath is not null && File.Exists(source.LocalPath))
            return source.LocalPath;

        foreach (var variant in source.Variants.OrderByDescending(v => v.Width))
        {
            if (!string.IsNullOrEmpty(variant.LocalPath) && File.Exists(variant.LocalPath))
                return variant.LocalPath;
        }

        return null;
    }

    private bool IsOwnedCoverPath(string? localPath, string folderName, Guid ownerId)
    {
        if (localPath is null)
            return false;

        var ownedDirectory = Path.GetFullPath(Path.Combine(_paths.Metadatas, folderName, $"{ownerId}"));
        var fullPath = Path.GetFullPath(localPath);
        return fullPath.StartsWith(
            ownedDirectory + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteCoverFiles(MetadataPicture picture)
    {
        foreach (var variant in picture.Variants)
        {
            if (variant.LocalPath is not null && File.Exists(variant.LocalPath))
                File.Delete(variant.LocalPath);
        }

        if (picture.LocalPath is not null && File.Exists(picture.LocalPath))
            File.Delete(picture.LocalPath);
    }
}
