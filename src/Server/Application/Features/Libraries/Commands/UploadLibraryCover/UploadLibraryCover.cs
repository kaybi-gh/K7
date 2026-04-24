using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.Libraries.Commands.UploadLibraryCover;

[Authorize(Roles = Roles.Administrator)]
public record UploadLibraryCoverCommand : IRequest<Guid>
{
    public required Guid LibraryId { get; init; }

    // Mode 1: upload a new file
    public Stream? FileStream { get; init; }
    public string? FileName { get; init; }

    // Mode 2: pick an existing MetadataPicture from within the library
    public Guid? SourcePictureId { get; init; }
}

public class UploadLibraryCoverCommandHandler : IRequestHandler<UploadLibraryCoverCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly ILogger<UploadLibraryCoverCommandHandler> _logger;

    public UploadLibraryCoverCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        IOptions<PathsConfiguration> pathsConfiguration,
        ILogger<UploadLibraryCoverCommandHandler> logger)
    {
        _context = context;
        _sender = sender;
        _pathsConfiguration = pathsConfiguration.Value;
        _logger = logger;
    }

    public async Task<Guid> Handle(UploadLibraryCoverCommand request, CancellationToken cancellationToken)
    {
        var library = await _context.Libraries
            .Include(l => l.CoverPicture)
            .FirstOrDefaultAsync(l => l.Id == request.LibraryId, cancellationToken);

        Guard.Against.NotFound(request.LibraryId, library);

        // Remove existing cover if any
        if (library.CoverPicture is not null)
        {
            _context.MetadataPictures.Remove(library.CoverPicture);
        }

        string localPath;

        if (request.FileStream is not null && request.FileName is not null)
        {
            var ext = Path.GetExtension(request.FileName);
            var directory = Path.Combine(_pathsConfiguration.Metadatas, "libraries", $"{request.LibraryId}");
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, $"cover{ext}");

            await using (var fs = File.Create(filePath))
            {
                await request.FileStream.CopyToAsync(fs, cancellationToken);
            }

            localPath = _pathsConfiguration.ToRelativeMetadataPath(filePath);
            _logger.LogInformation("Saved uploaded library cover for library {LibraryId} to {Path}", request.LibraryId, filePath);
        }
        else if (request.SourcePictureId is not null)
        {
            var source = await _context.MetadataPictures
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.SourcePictureId, cancellationToken);

            Guard.Against.NotFound(request.SourcePictureId.Value, source);
            localPath = source.LocalPath ?? string.Empty;
        }
        else
        {
            throw new ArgumentException("Either FileStream or SourcePictureId must be provided.");
        }

        var picture = new MetadataPicture
        {
            Id = Guid.NewGuid(),
            Type = MetadataPictureType.Cover,
            LibraryId = request.LibraryId,
            LocalPath = localPath
        };

        _context.MetadataPictures.Add(picture);
        await _context.SaveChangesAsync(cancellationToken);

        if (request.FileStream is not null)
        {
            await _sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new GenerateMetadataPictureVariantsCommand { MetadataPictureId = picture.Id },
                Priority = BackgroundTaskPriority.Normal,
                TargetEntityId = picture.Id,
                TargetEntityTypeName = nameof(MetadataPicture)
            }, cancellationToken);
        }

        return picture.Id;
    }
}
