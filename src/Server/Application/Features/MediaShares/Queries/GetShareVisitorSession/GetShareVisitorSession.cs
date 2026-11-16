using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Shares;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Shares;

namespace K7.Server.Application.Features.MediaShares.Queries.GetShareVisitorSession;

/// <summary>
/// What the current visitor may do in a share it already opened. Lets a page inside the
/// share reload without the link token.
/// </summary>
public record GetShareVisitorSessionQuery(Guid ShareId) : IRequest<OpenShareResultDto>;

public class GetShareVisitorSessionQueryHandler(
    IApplicationDbContext context,
    IShareVisitorAccess shareVisitorAccess)
    : IRequestHandler<GetShareVisitorSessionQuery, OpenShareResultDto>
{
    public async Task<OpenShareResultDto> Handle(GetShareVisitorSessionQuery request, CancellationToken cancellationToken)
    {
        var grant = shareVisitorAccess.RequireGrant(request.ShareId);

        var session = await context.MediaShareSessions
            .AsNoTracking()
            .Where(s => s.Id == grant.SessionId)
            .Select(s => new
            {
                s.ExpiresAt,
                s.Share.RootMediaId,
                RootMediaType = s.Share.RootMedia.Type,
                s.Share.AllowPlayback,
                s.Share.AllowDownload
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
            throw new NotFoundException(request.ShareId.ToString(), nameof(MediaShare));

        return new OpenShareResultDto
        {
            Outcome = OpenShareOutcome.Opened,
            Status = MediaShareStatus.Active,
            ShareId = request.ShareId,
            RootMediaId = session.RootMediaId,
            RootMediaType = session.RootMediaType,
            AllowPlayback = session.AllowPlayback,
            AllowDownload = session.AllowDownload,
            SessionExpiresAt = session.ExpiresAt
        };
    }
}
