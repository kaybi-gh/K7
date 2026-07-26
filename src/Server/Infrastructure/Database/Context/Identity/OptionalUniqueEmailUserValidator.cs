using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace K7.Server.Infrastructure.Database.Context.Identity;

/// <summary>
/// Allows null/empty email while still rejecting duplicate non-empty emails.
/// Identity's RequireUniqueEmail=true rejects empty emails, so we keep it false
/// and enforce uniqueness here when an email is provided.
/// </summary>
public sealed class OptionalUniqueEmailUserValidator : IUserValidator<ApplicationUser>
{
    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        var email = await manager.GetEmailAsync(user);
        if (string.IsNullOrWhiteSpace(email))
            return IdentityResult.Success;

        if (!new EmailAddressAttribute().IsValid(email))
            return IdentityResult.Failed(manager.ErrorDescriber.InvalidEmail(email));

        var owner = await manager.FindByEmailAsync(email);
        if (owner is null)
            return IdentityResult.Success;

        var ownerId = await manager.GetUserIdAsync(owner);
        var userId = await manager.GetUserIdAsync(user);
        if (string.Equals(ownerId, userId, StringComparison.Ordinal))
            return IdentityResult.Success;

        return IdentityResult.Failed(manager.ErrorDescriber.DuplicateEmail(email));
    }
}
