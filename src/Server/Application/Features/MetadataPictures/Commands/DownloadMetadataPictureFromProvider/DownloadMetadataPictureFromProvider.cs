using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Application.Common.Configuration;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.MetadataPictures.Commands.DownloadMetadataPictureFromProvider;

public record DownloadMetadataPictureFromProviderCommand : IRequest
{
    public required Guid Id { get; set; }
}

public class DownloadMetadataPictureFromProviderCommandHandler : IRequestHandler<DownloadMetadataPictureFromProviderCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly IImageProcessor _imageProcessor;
    private readonly ISender _sender;
    private readonly PathsConfiguration _pathsConfiguration;

    public DownloadMetadataPictureFromProviderCommandHandler(
        IApplicationDbContext context,
        HttpClient httpClient,
        IImageProcessor imageProcessor,
        ISender sender,
        IOptions<PathsConfiguration> pathsConfiguration)
    {
        _context = context;
        _httpClient = httpClient;
        _imageProcessor = imageProcessor;
        _sender = sender;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task Handle(DownloadMetadataPictureFromProviderCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.MetadataPictures
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        Guard.Against.NotFound(request.Id, entity);

        try
        {
            var basePath = _pathsConfiguration.Metadatas;

            var subDirectory = entity switch
            {
                { PersonId: not null } => Path.Combine("persons", $"{entity.PersonId}"),
                { PersonRoleId: not null } => Path.Combine("person-roles", $"{entity.PersonRoleId}"),
                { MediaId: not null } => Path.Combine("medias", $"{entity.MediaId}"),
                _ => throw new InvalidOperationException("No valid metadata id found.")
            };

            var directory = Path.Combine(basePath, subDirectory);
            var originalExtension = Path.GetExtension(entity.OriginalRemoteUri!.LocalPath);
            var tempFilePath = Path.Combine(directory, $"{entity.Id}{originalExtension}");

            var imageData = await _httpClient.GetByteArrayAsync(entity.OriginalRemoteUri.OriginalString, cancellationToken);
            var file = new FileInfo(tempFilePath);
            file.Directory?.Create();
            await File.WriteAllBytesAsync(tempFilePath, imageData, cancellationToken);

            // Convert to WebP
            var webpFilePath = Path.Combine(directory, $"{entity.Id}.webp");
            if (!string.Equals(originalExtension, ".webp", StringComparison.OrdinalIgnoreCase))
            {
                await _imageProcessor.ConvertToWebPAsync(tempFilePath, webpFilePath, cancellationToken: cancellationToken);
                File.Delete(tempFilePath);
            }
            else
            {
                webpFilePath = tempFilePath;
            }

            entity.LocalPath = webpFilePath;
            await _context.SaveChangesAsync(cancellationToken);

            // Enqueue variant generation as a background task
            if (entity.Type != MetadataPictureType.Thumbnail)
            {
                await _sender.Send(new CreateBackgroundTaskCommand
                {
                    Request = new GenerateMetadataPictureVariantsCommand
                    {
                        MetadataPictureId = entity.Id
                    },
                    Priority = BackgroundTaskPriority.Lowest,
                    TargetEntityId = entity.Id,
                    TargetEntityTypeName = nameof(MetadataPicture),
                    MaxRetryCount = 3
                }, cancellationToken);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Failed to download remote metadata picture. Network error: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new Exception($"Failed to save remote metadata picture to file. IO error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Unexpected error while trying to download remote metadata picture. Error: ${ex.Message}.");
        }
        // TODO - Tag failed picture download for later retry?
    }
}
