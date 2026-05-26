using K7.Server.Application.Common.Models;

namespace K7.Server.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserNameAsync(string userId);

    Task<bool> IsInRoleAsync(string userId, string role);

    Task<bool> AuthorizeAsync(string userId, string policyName);

    Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password);

    Task<Result> DeleteUserAsync(string userId);

    Task<string?> GetEmailAsync(string userId);

    Task<IList<string>> GetRolesAsync(string userId);

    Task SetRoleAsync(string userId, string role);

    Task ResetPasswordAsync(string userId, string newPassword);

    Task<bool> HasPasswordAsync(string userId);

    Task<bool> VerifyPasswordAsync(string userId, string password);

    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword);

    Task SetPasswordAsync(string userId, string newPassword);

    Task RemovePasswordAsync(string userId);

    Task UpdateEmailAsync(string userId, string newEmail);

    Task<IList<ExternalLoginInfo>> GetExternalLoginsAsync(string userId);

    Task RemoveExternalLoginAsync(string userId, string provider, string providerKey);

    Task AddExternalLoginAsync(string userId, string provider, string providerKey, string displayName);
}

public record ExternalLoginInfo(string LoginProvider, string ProviderKey, string? ProviderDisplayName);

