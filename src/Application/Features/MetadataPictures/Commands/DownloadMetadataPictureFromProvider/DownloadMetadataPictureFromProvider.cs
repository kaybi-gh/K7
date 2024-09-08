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
    private readonly PathsConfiguration _pathsConfiguration;

    public DownloadMetadataPictureFromProviderCommandHandler(IApplicationDbContext context, IOptions<PathsConfiguration> pathsConfiguration)
    {
        _context = context;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task Handle(DownloadMetadataPictureFromProviderCommand request, CancellationToken cancellationToken)
    {
        var destinationFilePath = _pathsConfiguration.Metadatas;
        var fileName = $"{request.MetadataPicture.Id}{Path.GetExtension(request.MetadataPicture.OriginalRemoteUri.LocalPath)}";

        if (request.MetadataPicture.PersonId != null)
        {
            destinationFilePath = Path.Combine(destinationFilePath, "persons", $"{request.MetadataPicture.PersonId}", fileName);
        }
        else if (request.MetadataPicture.PersonRoleId != null)
        {
            destinationFilePath = Path.Combine(destinationFilePath, "person-roles", $"{request.MetadataPicture.PersonRoleId}", fileName);
        }
        else if (request.MetadataPicture.MetadataId != null)
        {
            destinationFilePath = Path.Combine(destinationFilePath, "medias", $"{request.MetadataPicture.MetadataId}", fileName);
        }

        try
        {
            using var httpClient = new HttpClient();
            byte[] imageData = await httpClient.GetByteArrayAsync(request.MetadataPicture.OriginalRemoteUri.OriginalString, cancellationToken);
            FileInfo file = new(destinationFilePath);
            file.Directory!.Create();
            File.WriteAllBytes(destinationFilePath, imageData);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to download remote metadata picture, message: ${ex.Message}.");
            // TODO - Tag failed picture download for later retry?
        }
    }
}
