using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.Users.Commands.UploadUserAvatar;

public record UploadUserAvatarCommand : IRequest
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
}

public class UploadUserAvatarCommandHandler : IRequestHandler<UploadUserAvatarCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _currentUser;
    private readonly ISender _sender;
    private readonly PathsConfiguration _pathsConfiguration;
    private readonly ILogger<UploadUserAvatarCommandHandler> _logger;

    public UploadUserAvatarCommandHandler(
        IApplicationDbContext context,
        IUser currentUser,
        ISender sender,
        IOptions<PathsConfiguration> pathsConfiguration,
        ILogger<UploadUserAvatarCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _sender = sender;
        _pathsConfiguration = pathsConfiguration.Value;
        _logger = logger;
    }

    public async Task Handle(UploadUserAvatarCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(_currentUser.Id);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == _currentUser.Id, cancellationToken);

        Guard.Against.NotFound(_currentUser.Id.Value, user);

        // Remove existing avatar picture if any
        var existingPicture = await _context.MetadataPictures
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.Type == MetadataPictureType.UserAvatar, cancellationToken);

        if (existingPicture is not null)
        {
            if (File.Exists(existingPicture.LocalPath))
                File.Delete(existingPicture.LocalPath);

            _context.MetadataPictures.Remove(existingPicture);
        }

        var ext = Path.GetExtension(request.FileName);
        var pictureId = Guid.NewGuid();
        var directory = Path.Combine(_pathsConfiguration.Metadatas, "users", $"{user.Id}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"{pictureId}{ext}");

        await using (var fs = File.Create(filePath))
        {
            await request.FileStream.CopyToAsync(fs, cancellationToken);
        }

        _logger.LogInformation("Saved avatar for user {UserId} to {Path}", user.Id, filePath);

        var picture = new MetadataPicture
        {
            Id = pictureId,
            Type = MetadataPictureType.UserAvatar,
            UserId = user.Id,
            LocalPath = filePath
        };

        _context.MetadataPictures.Add(picture);
        await _context.SaveChangesAsync(cancellationToken);

        await _sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new GenerateMetadataPictureVariantsCommand { MetadataPictureId = picture.Id },
            Priority = BackgroundTaskPriority.Normal,
            TargetEntityId = picture.Id,
            TargetEntityTypeName = nameof(MetadataPicture),
            ConcurrencyGroup = "image-processing"
        }, cancellationToken);
    }
}
