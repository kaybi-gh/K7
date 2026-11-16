using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Shares;

namespace K7.Server.Application.Features.MediaShares.Commands.RegenerateMediaShareLink;

/// <summary>
/// Issues a new link for a share. The previous link stops working and every visitor
/// has to open the new one. Used when a link was lost: only its hash is stored.
/// </summary>
[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record RegenerateMediaShareLinkCommand(Guid Id) : IRequest<MediaShareLinkDto>;

public class RegenerateMediaShareLinkCommandHandler(
    IApplicationDbContext context,
    IMediaShareManager shareManager,
    IShareAccessInvalidator invalidator)
    : IRequestHandler<RegenerateMediaShareLinkCommand, MediaShareLinkDto>
{
    public async Task<MediaShareLinkDto> Handle(RegenerateMediaShareLinkCommand request, CancellationToken cancellationToken)
    {
        await shareManager.EnsureEnabledAsync(cancellationToken);
        var actor = await shareManager.RequireSharingActorAsync(cancellationToken);
        var share = await shareManager.GetManagedShareAsync(request.Id, actor, cancellationToken);

        var token = ShareTokenHelper.Generate();
        share.TokenHash = ShareTokenHelper.Hash(token);
        await context.SaveChangesAsync(cancellationToken);

        await context.MediaShareSessions
            .Where(s => s.ShareId == share.Id)
            .ExecuteDeleteAsync(cancellationToken);
        invalidator.Invalidate();

        return new MediaShareLinkDto { Id = share.Id, Token = token };
    }
}
