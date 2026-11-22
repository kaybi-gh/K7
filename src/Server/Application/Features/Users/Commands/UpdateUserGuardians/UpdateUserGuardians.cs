using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.UpdateUserGuardians;

/// <summary>
/// Replaces the guardians of an account. An empty list makes the account unmanaged again.
/// </summary>
[Authorize(Roles = Roles.Administrator)]
public record UpdateUserGuardiansCommand : IRequest
{
    public required Guid Id { get; init; }
    public required IReadOnlyList<Guid> GuardianUserIds { get; init; }
}

public class UpdateUserGuardiansCommandHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<UpdateUserGuardiansCommand>
{
    public async Task Handle(UpdateUserGuardiansCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, user);

        var existing = await context.UserGuardians
            .Where(g => g.UserId == request.Id)
            .ToListAsync(cancellationToken);

        if (request.GuardianUserIds.Count > 0)
        {
            await EnsureCanBeManagedAsync(user);
            await EnsureCanBeGuardiansAsync(request.GuardianUserIds, cancellationToken);
        }

        var wanted = request.GuardianUserIds.ToHashSet();

        context.UserGuardians.RemoveRange(existing.Where(g => !wanted.Contains(g.GuardianUserId)));

        var kept = existing.Select(g => g.GuardianUserId).ToHashSet();
        context.UserGuardians.AddRange(wanted
            .Where(guardianUserId => !kept.Contains(guardianUserId))
            .Select(guardianUserId => new UserGuardian
            {
                Id = Guid.NewGuid(),
                UserId = request.Id,
                GuardianUserId = guardianUserId
            }));

        await context.SaveChangesAsync(cancellationToken);
    }

    // A guardian gets a full session of the managed account, so that account must stay an
    // ordinary user: managing an administrator would hand admin rights to its guardians.
    private async Task EnsureCanBeManagedAsync(User user)
    {
        if (user.IdentityUserId is null || user.IsVirtualUser)
            throw Invalid(nameof(UpdateUserGuardiansCommand.Id), "This account cannot be managed.");

        var roles = await identityService.GetRolesAsync(user.IdentityUserId);
        if (roles.Contains(Roles.Administrator))
            throw Invalid(nameof(UpdateUserGuardiansCommand.Id), "An administrator account cannot be managed.");

        if (!roles.Contains(Roles.User))
            throw Invalid(nameof(UpdateUserGuardiansCommand.Id), "This account cannot be managed.");
    }

    private async Task EnsureCanBeGuardiansAsync(IReadOnlyList<Guid> guardianUserIds, CancellationToken cancellationToken)
    {
        var guardians = await context.Users
            .AsNoTracking()
            .Where(u => guardianUserIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        if (guardians.Count != guardianUserIds.Count)
            throw Invalid(nameof(UpdateUserGuardiansCommand.GuardianUserIds), "A guardian account does not exist.");

        foreach (var guardian in guardians)
        {
            if (guardian.IdentityUserId is null || guardian.IsVirtualUser)
                throw Invalid(nameof(UpdateUserGuardiansCommand.GuardianUserIds), "A guardian must be a local account.");

            var roles = await identityService.GetRolesAsync(guardian.IdentityUserId);
            if (!roles.Contains(Roles.Administrator) && !roles.Contains(Roles.User))
                throw Invalid(nameof(UpdateUserGuardiansCommand.GuardianUserIds), "A guardian must be a user or an administrator.");
        }
    }

    private static ValidationException Invalid(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
