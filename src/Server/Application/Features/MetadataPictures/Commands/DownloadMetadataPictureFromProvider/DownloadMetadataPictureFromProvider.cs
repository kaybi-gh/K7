using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Infrastructure.Configuration;
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
    private readonly PathsConfiguration _pathsConfiguration;

    public DownloadMetadataPictureFromProviderCommandHandler(IApplicationDbContext context, HttpClient httpClient, IOptions<PathsConfiguration> pathsConfiguration)
    {
        _context = context;
        _httpClient = httpClient;
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
            var fileName = $"{entity.Id}{Path.GetExtension(entity.OriginalRemoteUri!.LocalPath)}";

            var subDirectory = entity switch
            {
                { PersonId: not null } => Path.Combine("persons", $"{entity.PersonId}"),
                { PersonRoleId: not null } => Path.Combine("person-roles", $"{entity.PersonRoleId}"),
                { MetadataId: not null } => Path.Combine("medias", $"{entity.MetadataId}"),
                _ => throw new InvalidOperationException("No valid metadata id found.")
            };

            var filePath = Path.Combine(basePath, subDirectory, fileName);
        
            var imageData = await _httpClient.GetByteArrayAsync(entity.OriginalRemoteUri.OriginalString, cancellationToken);
            var file = new FileInfo(filePath);
            file.Directory?.Create();
            await File.WriteAllBytesAsync(filePath, imageData, cancellationToken);

            entity.LocalPath = filePath;
            await _context.SaveChangesAsync(cancellationToken);
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
