using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Downloads.Commands.DeleteDownload;

public record DeleteDownloadCommand(Guid Id) : IRequest;

public class DeleteDownloadCommandHandler : IRequestHandler<DeleteDownloadCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public DeleteDownloadCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(DeleteDownloadCommand request, CancellationToken cancellationToken)
    {
        var download = await _context.Downloads
            .FirstOrDefaultAsync(d => d.Id == request.Id && d.UserId == _user.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, download);

        // Clean up transcoded output file if it exists and is not the original
        if (!download.IsDirectStream && download.OutputPath is not null && File.Exists(download.OutputPath))
        {
            File.Delete(download.OutputPath);
        }

        _context.Downloads.Remove(download);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
