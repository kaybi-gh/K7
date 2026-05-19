using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Downloads.Commands.TranscodeDownload;

public record TranscodeDownloadCommand : IRequest
{
    public required Guid DownloadId { get; init; }
}

public class TranscodeDownloadCommandHandler : IRequestHandler<TranscodeDownloadCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediaTranscoder _mediaTranscoder;
    private readonly ILogger<TranscodeDownloadCommandHandler> _logger;

    public TranscodeDownloadCommandHandler(
        IApplicationDbContext context,
        IMediaTranscoder mediaTranscoder,
        ILogger<TranscodeDownloadCommandHandler> logger)
    {
        _context = context;
        _mediaTranscoder = mediaTranscoder;
        _logger = logger;
    }

    public async Task Handle(TranscodeDownloadCommand request, CancellationToken cancellationToken)
    {
        var download = await _context.Downloads
            .Include(d => d.IndexedFile)
                .ThenInclude(f => f.FileMetadata)
            .FirstOrDefaultAsync(d => d.Id == request.DownloadId, cancellationToken);

        Guard.Against.NotFound(request.DownloadId, download);

        if (download.Status != DownloadStatus.Transcoding)
        {
            _logger.LogWarning("Download {DownloadId} is not in Transcoding status, skipping", request.DownloadId);
            return;
        }

        var inputPath = download.IndexedFile.Path;
        var outputDir = Path.Combine(Path.GetTempPath(), "k7-downloads");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"{download.Id:N}.mp4");

        try
        {
            var audioTrackIndex = download.AudioTrackIndex ?? 0;

            await _mediaTranscoder.RemuxWithAudioTranscodeAsync(
                inputPath,
                outputPath,
                audioTrackIndex,
                cancellationToken);

            download.OutputPath = outputPath;
            download.IsDirectStream = false;
            download.FileSize = new FileInfo(outputPath).Length;
            download.ContentType = "video/mp4";
            download.Status = DownloadStatus.Ready;
            download.ReadyAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Download {DownloadId} transcode completed: {OutputPath}", request.DownloadId, outputPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Download {DownloadId} transcode failed", request.DownloadId);

            download.Status = DownloadStatus.Failed;
            download.FailureReason = ex.Message;
            await _context.SaveChangesAsync(cancellationToken);

            throw;
        }
    }
}
