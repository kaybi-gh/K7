using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Shares;
using K7.Shared.Dtos.Shares;

namespace K7.Server.Application.Features.MediaShares.Queries.GetMediaShare;

/// <summary>
/// One share with its most recent visitors.
/// </summary>
[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetMediaShareQuery(Guid Id) : IRequest<MediaShareDto>;

public class GetMediaShareQueryHandler(
    IApplicationDbContext context,
    IIdentityService identityService,
    IMediaShareManager shareManager)
    : IRequestHandler<GetMediaShareQuery, MediaShareDto>
{
    public async Task<MediaShareDto> Handle(GetMediaShareQuery request, CancellationToken cancellationToken)
    {
        var actor = await shareManager.RequireActorAsync(cancellationToken);

        var share = await context.MediaShares
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (share is null || (!actor.IsAdmin && share.CreatedByUserId != actor.UserId))
            throw new NotFoundException(request.Id.ToString(), nameof(MediaShare));

        var dto = (await MediaShareDtoBuilder.BuildAsync(context, identityService, shareManager, [share], cancellationToken))[0];

        var sessions = await context.MediaShareSessions
            .AsNoTracking()
            .Where(s => s.ShareId == share.Id)
            .OrderByDescending(s => s.CreatedAt)
            .Take(MediaShareRules.MaxListedSessions)
            .Select(s => new MediaShareSessionDto
            {
                Id = s.Id,
                CreatedAt = s.CreatedAt,
                LastSeenAt = s.LastSeenAt,
                ExpiresAt = s.ExpiresAt,
                IpAddress = s.IpAddress,
                UserAgent = s.UserAgent
            })
            .ToListAsync(cancellationToken);

        return dto with { Sessions = sessions };
    }
}
