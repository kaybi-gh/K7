using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.Downloads.Queries.GetDownloadFile;

public record GetDownloadFileQuery(Guid Id) : IRequest<HttpContentResult>;

public class GetDownloadFileQueryHandler : IRequestHandler<GetDownloadFileQuery, HttpContentResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly PathsConfiguration _pathsConfiguration;

    public GetDownloadFileQueryHandler(
        IApplicationDbContext context,
        IUser user,
        IOptions<PathsConfiguration> pathsConfiguration)
    {
        _context = context;
        _user = user;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task<HttpContentResult> Handle(GetDownloadFileQuery request, CancellationToken cancellationToken)
    {
        var download = await _context.Downloads
            .Include(d => d.IndexedFile)
            .FirstOrDefaultAsync(d => d.Id == request.Id && d.UserId == _user.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, download);

        if (download.Status != DownloadStatus.Ready)
        {
            return new ConflictHttpContentResult(new { Message = "Download is not ready yet.", Status = download.Status.ToString() });
        }

        var filePath = download.OutputPath;
        Guard.Against.NullOrEmpty(filePath);

        var allowedRoots = new List<string> { _pathsConfiguration.Transcoding };
        var libraryRootPath = await _context.Libraries
            .AsNoTracking()
            .Where(l => l.Id == download.IndexedFile.LibraryId)
            .Select(l => l.RootPath)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(libraryRootPath))
            allowedRoots.Add(libraryRootPath);

        PathContainmentHelper.EnsurePathContained(filePath, allowedRoots, "Download path is outside allowed media roots.");

        var file = new FileInfo(filePath);
        if (!file.Exists)
        {
            return new EmptyHttpContentResult(404);
        }

        var contentType = download.ContentType ?? "application/octet-stream";
        var fileName = Path.GetFileName(download.IndexedFile.Path);

        return new FileHttpContentResult(filePath, contentType, FileDownloadName: fileName);
    }
}
