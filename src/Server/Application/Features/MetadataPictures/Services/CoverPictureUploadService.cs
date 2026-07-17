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

    Task<string> ResolveSourcePicturePathAsync(
        Guid sourcePictureId,
        Func<MetadataPicture, CancellationToken, Task>? authorizeAsync = null,
        CancellationToken cancellationToken = default);

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
        var directory = Path.Combine(_paths.Metadatas, folderName, $"{ownerId}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"cover{ext}");

        await using (var fs = File.Create(filePath))
        {
            await fileStream.CopyToAsync(fs, cancellationToken);
        }

        logger.LogInformation("Saved uploaded cover for {OwnerId} under {Folder} to {Path}", ownerId, folderName, filePath);
        return _paths.ToRelativeMetadataPath(filePath);
    }

    public async Task<string> ResolveSourcePicturePathAsync(
        Guid sourcePictureId,
        Func<MetadataPicture, CancellationToken, Task>? authorizeAsync = null,
        CancellationToken cancellationToken = default)
    {
        var source = await context.MetadataPictures
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == sourcePictureId, cancellationToken);

        Guard.Against.NotFound(sourcePictureId, source);

        if (authorizeAsync is not null)
        {
            await authorizeAsync(source, cancellationToken);
        }

        return source.LocalPath ?? string.Empty;
    }

    public Task EnqueueVariantGenerationAsync(Guid metadataPictureId, CancellationToken cancellationToken = default)
    {
        return sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new GenerateMetadataPictureVariantsCommand { MetadataPictureId = metadataPictureId },
            Priority = BackgroundTaskPriority.Normal,
            TargetEntityId = metadataPictureId,
            TargetEntityTypeName = nameof(MetadataPicture)
        }, cancellationToken);
    }
}
