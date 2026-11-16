using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.MediaShares.Queries.GetShareDownloadFile;

/// <summary>
/// The original file of a shared media, as a download. No transcoding: the visitor gets
/// the file as it is on disk.
/// </summary>
/// <param name="CountAsConsumed">False for a HEAD request: asking about the file is not taking it.</param>
public record GetShareDownloadFileQuery(Guid ShareId, Guid IndexedFileId, bool CountAsConsumed = true) : IRequest<HttpContentResult>;

public class GetShareDownloadFileQueryHandler(
    IApplicationDbContext context,
    IShareVisitorAccess shareVisitorAccess,
    IMediaAccessGuard accessGuard)
    : IRequestHandler<GetShareDownloadFileQuery, HttpContentResult>
{
    public async Task<HttpContentResult> Handle(GetShareDownloadFileQuery request, CancellationToken cancellationToken)
    {
        shareVisitorAccess.RequireGrant(request.ShareId);
        await accessGuard.EnsureAccessByIndexedFileAsync(request.IndexedFileId, cancellationToken);

        var matches = await shareVisitorAccess.GetMatchingSharesByIndexedFileAsync(request.IndexedFileId, cancellationToken);
        var match = matches.FirstOrDefault(m => m.ShareId == request.ShareId);
        if (match is null)
            throw new NotFoundException(request.IndexedFileId.ToString(), "IndexedFile");

        if (!match.AllowDownload)
            throw new ForbiddenAccessException();

        var file = await context.IndexedFiles
            .AsNoTracking()
            .Include(f => f.FileMetadata)
            .FirstOrDefaultAsync(f => f.Id == request.IndexedFileId, cancellationToken);

        if (file is null || string.IsNullOrEmpty(file.Path) || !File.Exists(file.Path))
            return new EmptyHttpContentResult(404);

        var container = file.FileMetadata?.Container;
        var mimeType = container is not null && Constants.ContainerMimeTypeMapping.TryGetValue(container, out var mime)
            ? mime
            : "application/octet-stream";

        // A download is a full copy of the media: it counts as consumed.
        if (request.CountAsConsumed && file.MediaId is { } mediaId)
            await ShareConsumption.MarkConsumedAsync(context, request.ShareId, mediaId, cancellationToken);

        return new FileHttpContentResult(
            file.Path,
            mimeType,
            FileDownloadName: Path.GetFileName(file.Path),
            LongRunning: true);
    }
}
