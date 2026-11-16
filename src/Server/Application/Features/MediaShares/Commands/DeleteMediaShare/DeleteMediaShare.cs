using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.MediaShares.Commands.DeleteMediaShare;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record DeleteMediaShareCommand(Guid Id) : IRequest;

public class DeleteMediaShareCommandHandler(
    IApplicationDbContext context,
    IMediaShareManager shareManager,
    IShareAccessInvalidator invalidator)
    : IRequestHandler<DeleteMediaShareCommand>
{
    public async Task Handle(DeleteMediaShareCommand request, CancellationToken cancellationToken)
    {
        var actor = await shareManager.RequireActorAsync(cancellationToken);
        var share = await shareManager.GetManagedShareAsync(request.Id, actor, cancellationToken);

        // Visitor devices are not cascaded with the sessions: drop them here.
        var deviceIds = await context.MediaShareSessions
            .Where(s => s.ShareId == share.Id && s.DeviceId != null)
            .Select(s => s.DeviceId!.Value)
            .ToListAsync(cancellationToken);

        context.MediaShares.Remove(share);
        await context.SaveChangesAsync(cancellationToken);
        invalidator.Invalidate();

        if (deviceIds.Count > 0)
        {
            await context.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
