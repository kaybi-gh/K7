using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Entities;
using MediaServer.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace MediaServer.Application.Features.MetadataPictures.Commands.DownloadMetadataPictureFromProvider;

public record DownloadMetadataPictureFromProviderCommand : IRequest
{
    public required MetadataPicture MetadataPicture { get; set; }
}

public class DownloadMetadataPictureFromProviderCommandHandler : IRequestHandler<DownloadMetadataPictureFromProviderCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly PathsConfiguration _pathsConfiguration;

    public DownloadMetadataPictureFromProviderCommandHandler(IApplicationDbContext context, HttpClient httpClient, IOptions<PathsConfiguration> pathsConfiguration)
    {
        _context = context;
        _httpClient = httpClient;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task Handle(DownloadMetadataPictureFromProviderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var basePath = _pathsConfiguration.Metadatas;
            var fileName = $"{request.MetadataPicture.Id}{Path.GetExtension(request.MetadataPicture.OriginalRemoteUri.LocalPath)}";

            var subDirectory = request.MetadataPicture switch
            {
                { PersonId: not null } => Path.Combine("persons", $"{request.MetadataPicture.PersonId}"),
                { PersonRoleId: not null } => Path.Combine("person-roles", $"{request.MetadataPicture.PersonRoleId}"),
                { MetadataId: not null } => Path.Combine("medias", $"{request.MetadataPicture.MetadataId}"),
                _ => throw new InvalidOperationException("No valid metadata id found.")
            };

            var filePath = Path.Combine(basePath, subDirectory, fileName);
        
            var imageData = await _httpClient.GetByteArrayAsync(request.MetadataPicture.OriginalRemoteUri.OriginalString, cancellationToken);
            var file = new FileInfo(filePath);
            file.Directory?.Create();
            await File.WriteAllBytesAsync(filePath, imageData, cancellationToken);
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
