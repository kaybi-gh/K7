using System.Security.Cryptography;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Users.Commands.UpdateUserPin;

public record UpdateUserPinCommand : IRequest
{
    public required Guid Id { get; init; }
    public string? Pin { get; init; }
}

public class UpdateUserPinCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService) : IRequestHandler<UpdateUserPinCommand>
{
    public async Task Handle(UpdateUserPinCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);
        Guard.Against.NullOrEmpty(currentUser.IdentityId);

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, user);

        if (currentUser.Id != request.Id)
        {
            var isAdmin = await identityService.IsInRoleAsync(currentUser.IdentityId, Roles.Administrator);
            if (!isAdmin || request.Pin is not null)
                throw new ForbiddenAccessException();
        }

        user.PinHash = request.Pin is null ? null : HashPin(request.Pin);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string HashPin(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, 10_000, HashAlgorithmName.SHA256, 32);
        return $"$PBKDF2$iterations=10000${Convert.ToHexStringLower(salt)}${Convert.ToHexStringLower(hash)}";
    }
}
