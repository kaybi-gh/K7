using System.Net;
using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.MetadataPictures.Commands.DownloadMetadataPictureFromProvider;

public record DownloadMetadataPictureFromProviderCommand : IRequest
{
    public required Guid Id { get; set; }
}

public class DownloadMetadataPictureFromProviderCommandHandler : IRequestHandler<DownloadMetadataPictureFromProviderCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IImageProcessor _imageProcessor;
    private readonly ISender _sender;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly OutboundRateLimiter _rateLimiter;
    private readonly MediaPictureReadyNotifier _pictureReadyNotifier;

    public DownloadMetadataPictureFromProviderCommandHandler(
        IApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        IImageProcessor imageProcessor,
        ISender sender,
        IOptions<PathsConfiguration> pathsConfiguration,
        OutboundRateLimiter rateLimiter,
        MediaPictureReadyNotifier pictureReadyNotifier)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _imageProcessor = imageProcessor;
        _sender = sender;
        _pathsConfiguration = pathsConfiguration.Value;
        _rateLimiter = rateLimiter;
        _pictureReadyNotifier = pictureReadyNotifier;
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

            await _rateLimiter.WaitAsync(entity.OriginalRemoteUri!.Host, cancellationToken);

            using var httpClient = _httpClientFactory.CreateClient(DependencyInjection.MetadataPictureDownloadClient);
            using var response = await httpClient.GetAsync(entity.OriginalRemoteUri.OriginalString, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(5);
                _rateLimiter.ReportRetryAfter(entity.OriginalRemoteUri.Host, retryAfter);
                throw new HttpRequestException($"Rate limited (429) by {entity.OriginalRemoteUri.Host}. Retry after {retryAfter.TotalSeconds}s.");
            }

            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var downloadedExtension = MetadataImageUrlHelper.GetExtensionFromContentType(contentType)
                ?? Path.GetExtension(entity.OriginalRemoteUri.LocalPath);

            if (string.IsNullOrEmpty(downloadedExtension))
                downloadedExtension = ".jpg";

            var tempFilePath = Path.Combine(directory, $"{entity.Id}{downloadedExtension}");
            var imageData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var file = new FileInfo(tempFilePath);
            file.Directory?.Create();
            await File.WriteAllBytesAsync(tempFilePath, imageData, cancellationToken);

            var isSvg = _imageProcessor.IsSvgFile(tempFilePath)
                || MetadataImageUrlHelper.IsVectorContentType(contentType);

            if (isSvg)
            {
                entity.LocalPath = tempFilePath;
            }
            else
            {
                var webpFilePath = Path.Combine(directory, $"{entity.Id}.webp");
                if (!string.Equals(downloadedExtension, ".webp", StringComparison.OrdinalIgnoreCase))
                {
                    await _imageProcessor.ConvertToWebPAsync(tempFilePath, webpFilePath, cancellationToken: cancellationToken);
                    File.Delete(tempFilePath);
                }
                else
                {
                    webpFilePath = tempFilePath;
                }

                entity.LocalPath = webpFilePath;
            }

            await _context.SaveChangesAsync(cancellationToken);

            await _pictureReadyNotifier.NotifyIfMediaPictureReadyAsync(entity.Id, cancellationToken);

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
                    MaxAttempts = 3,
                    ConcurrencyGroup = "image-processing"
                }, cancellationToken);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Failed to download remote metadata picture. Network error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new Exception($"Timed out downloading remote metadata picture from {entity.OriginalRemoteUri?.Host}.", ex);
        }
        catch (IOException ex)
        {
            throw new Exception($"Failed to save remote metadata picture to file. IO error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Unexpected error while trying to download remote metadata picture. Error: {ex.Message}.", ex);
        }
    }
}
