using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Users.Queries.GetGuardianSessionTarget;

/// <summary>
/// Resolves the managed account a guardian asks a device session for. The guardian is passed
/// explicitly: the device-link endpoint authenticates the browser cookie itself.
/// Throws <see cref="ForbiddenAccessException"/> when the guardian may not open that session.
/// The account PIN is not part of this request on purpose: a failing request is logged whole,
/// so the caller checks the PIN with <c>VerifyUserPinCommand</c>, which never throws on a mismatch.
/// </summary>
public record GetGuardianSessionTargetQuery(string GuardianIdentityUserId, Guid TargetUserId)
    : IRequest<GuardianSessionTarget>;

public record GuardianSessionTarget(Guid GuardianUserId, Guid UserId, string IdentityUserId, bool HasPin);

public class GetGuardianSessionTargetQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetGuardianSessionTargetQuery, GuardianSessionTarget>
{
    public async Task<GuardianSessionTarget> Handle(GetGuardianSessionTargetQuery request, CancellationToken cancellationToken)
    {
        var guardian = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdentityUserId == request.GuardianIdentityUserId, cancellationToken);

        if (guardian is null || !guardian.IsActive)
            throw new ForbiddenAccessException();

        var target = await context.UserGuardians
            .AsNoTracking()
            .Where(g => g.GuardianUserId == guardian.Id && g.UserId == request.TargetUserId)
            .Select(g => g.User)
            .FirstOrDefaultAsync(cancellationToken);

        if (target is null || !target.IsActive || target.IdentityUserId is null || target.IsVirtualUser)
            throw new ForbiddenAccessException();

        // Checked again here, not only when the link is set: the role may have changed since.
        var roles = await identityService.GetRolesAsync(target.IdentityUserId);
        if (roles.Contains(Roles.Administrator) || !roles.Contains(Roles.User))
            throw new ForbiddenAccessException();

        return new GuardianSessionTarget(guardian.Id, target.Id, target.IdentityUserId, target.PinHash is not null);
    }
}
