using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Downloads.Commands.TranscodeDownload;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Downloads.Commands.PrepareDownload;

public record PrepareDownloadCommand : IRequest<DownloadDto>
{
    public required Guid IndexedFileId { get; init; }
    public required Guid DeviceId { get; init; }
    public int? AudioTrackIndex { get; init; }
    public int[]? SubtitleTrackIndices { get; init; }
}

public class PrepareDownloadCommandHandler : IRequestHandler<PrepareDownloadCommand, DownloadDto>
{
    private static readonly HashSet<string> DirectPlayAudioCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "aac", "mp3", "ac3", "pcm_s16le", "pcm_s24le", "flac", "alac", "vorbis", "opus"
    };

    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IMediaAccessGuard _accessGuard;
    private readonly ISender _sender;

    public PrepareDownloadCommandHandler(IApplicationDbContext context, IUser user, IMediaAccessGuard accessGuard, ISender sender)
    {
        _context = context;
        _user = user;
        _accessGuard = accessGuard;
        _sender = sender;
    }

    public async Task<DownloadDto> Handle(PrepareDownloadCommand request, CancellationToken cancellationToken)
    {
        await _accessGuard.EnsureAccessByIndexedFileAsync(request.IndexedFileId, cancellationToken);

        var indexedFile = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
                .ThenInclude(x => (x as VideoFileMetadata)!.AudioTracks)
            .FirstOrDefaultAsync(x => x.Id == request.IndexedFileId, cancellationToken);

        Guard.Against.NotFound(request.IndexedFileId, indexedFile);

        var container = indexedFile.FileMetadata?.Container;
        var mimeType = container is not null && Constants.ContainerMimeTypeMapping.TryGetValue(container, out var mime)
            ? mime
            : "application/octet-stream";

        // Check for existing ready download for same file+device+tracks
        var existing = await _context.Downloads
            .FirstOrDefaultAsync(d =>
                d.IndexedFileId == request.IndexedFileId &&
                d.DeviceId == request.DeviceId &&
                d.AudioTrackIndex == request.AudioTrackIndex &&
                (d.Status == DownloadStatus.Ready || d.Status == DownloadStatus.Pending || d.Status == DownloadStatus.Transcoding),
                cancellationToken);

        if (existing is not null)
        {
            return ToDto(existing);
        }

        var needsAudioTranscode = NeedsAudioTranscode(indexedFile, request.AudioTrackIndex);

        var download = new Download
        {
            Id = Guid.NewGuid(),
            IndexedFileId = request.IndexedFileId,
            DeviceId = request.DeviceId,
            UserId = _user.Id,
            AudioTrackIndex = request.AudioTrackIndex,
            SubtitleTrackIndices = request.SubtitleTrackIndices is { Length: > 0 }
                ? string.Join(",", request.SubtitleTrackIndices)
                : null,
            IsDirectStream = !needsAudioTranscode
        };

        if (needsAudioTranscode)
        {
            download.Status = DownloadStatus.Transcoding;
            _context.Downloads.Add(download);
            await _context.SaveChangesAsync(cancellationToken);

            await _sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new TranscodeDownloadCommand { DownloadId = download.Id },
                TargetEntityId = download.Id,
                Priority = BackgroundTaskPriority.High,
                MaxAttempts = 2,
                TimeoutSeconds = (int)TimeSpan.FromHours(2).TotalSeconds,
                ConcurrencyGroup = "download-transcode"
            }, cancellationToken);
        }
        else
        {
            download.Status = DownloadStatus.Ready;
            download.OutputPath = indexedFile.Path;
            download.ContentType = mimeType;
            download.FileSize = indexedFile.Size;
            download.ReadyAt = DateTimeOffset.UtcNow;
            _context.Downloads.Add(download);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return ToDto(download);
    }

    private static bool NeedsAudioTranscode(IndexedFile indexedFile, int? audioTrackIndex)
    {
        if (indexedFile.FileMetadata is not VideoFileMetadata videoMetadata)
            return false;

        var trackIndex = audioTrackIndex ?? 0;
        var audioTrack = videoMetadata.AudioTracks
            .OrderBy(t => t.Index)
            .ElementAtOrDefault(trackIndex);

        if (audioTrack is null)
            return false;

        return !DirectPlayAudioCodecs.Contains(audioTrack.Codec);
    }

    private static DownloadDto ToDto(Download download)
    {
        return new DownloadDto
        {
            Id = download.Id,
            IndexedFileId = download.IndexedFileId,
            DeviceId = download.DeviceId,
            Status = download.Status,
            IsDirectStream = download.IsDirectStream,
            FileSize = download.FileSize,
            ContentType = download.ContentType,
            ReadyAt = download.ReadyAt,
            FailureReason = download.FailureReason
        };
    }
}
