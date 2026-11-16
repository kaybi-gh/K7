using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Shared.Dtos.Shares;

namespace K7.Server.Application.Features.MediaShares.Queries.GetMediaSharingAvailability;

/// <summary>
/// Tells a client whether to offer sharing to the current user: the server feature must
/// be on and the user must hold the share capability (role default or override).
/// </summary>
[Authorize]
public record GetMediaSharingAvailabilityQuery : IRequest<MediaSharingAvailabilityDto>;

public class GetMediaSharingAvailabilityQueryHandler(IMediaShareManager shareManager)
    : IRequestHandler<GetMediaSharingAvailabilityQuery, MediaSharingAvailabilityDto>
{
    public async Task<MediaSharingAvailabilityDto> Handle(GetMediaSharingAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var enabled = await shareManager.IsEnabledAsync(cancellationToken);
        var actor = await shareManager.RequireActorAsync(cancellationToken);
        var canShare = await shareManager.CanUserShareAsync(actor.UserId, cancellationToken);

        return new MediaSharingAvailabilityDto
        {
            Enabled = enabled,
            CanShare = canShare,
            IsAdmin = actor.IsAdmin
        };
    }
}
