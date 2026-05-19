using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Downloads.Queries.GetDownload;

public record GetDownloadQuery(Guid Id) : IRequest<DownloadDto>;

public class GetDownloadQueryHandler : IRequestHandler<GetDownloadQuery, DownloadDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetDownloadQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<DownloadDto> Handle(GetDownloadQuery request, CancellationToken cancellationToken)
    {
        var download = await _context.Downloads
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.Id && d.UserId == _user.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, download);

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
