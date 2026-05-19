using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.Features.Downloads.Queries.GetDownloadFile;

public record GetDownloadFileQuery(Guid Id) : IRequest<IResult>;

public class GetDownloadFileQueryHandler : IRequestHandler<GetDownloadFileQuery, IResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetDownloadFileQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<IResult> Handle(GetDownloadFileQuery request, CancellationToken cancellationToken)
    {
        var download = await _context.Downloads
            .Include(d => d.IndexedFile)
            .FirstOrDefaultAsync(d => d.Id == request.Id && d.UserId == _user.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, download);

        if (download.Status != DownloadStatus.Ready)
        {
            return Results.Conflict(new { Message = "Download is not ready yet.", Status = download.Status.ToString() });
        }

        var filePath = download.OutputPath;
        Guard.Against.NullOrEmpty(filePath);

        var file = new FileInfo(filePath);
        if (!file.Exists)
        {
            return Results.NotFound();
        }

        var contentType = download.ContentType ?? "application/octet-stream";
        var fileName = Path.GetFileName(download.IndexedFile.Path);

        return Results.File(filePath, contentType: contentType, fileDownloadName: fileName, enableRangeProcessing: true);
    }
}
