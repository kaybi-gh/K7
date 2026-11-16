using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.MediaShares.Commands.RevokeMediaShare;

/// <summary>
/// Closes a share for everyone at once, visitors already in included.
/// The share stays listed. Owners may always revoke, even after losing the right to share.
/// </summary>
[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record RevokeMediaShareCommand(Guid Id) : IRequest;

public class RevokeMediaShareCommandHandler(
    IApplicationDbContext context,
    IMediaShareManager shareManager,
    IShareAccessInvalidator invalidator)
    : IRequestHandler<RevokeMediaShareCommand>
{
    public async Task Handle(RevokeMediaShareCommand request, CancellationToken cancellationToken)
    {
        var actor = await shareManager.RequireActorAsync(cancellationToken);
        var share = await shareManager.GetManagedShareAsync(request.Id, actor, cancellationToken);

        if (share.RevokedAt is not null)
            return;

        share.RevokedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        invalidator.Invalidate();
    }
}
