using K7.Server.Application.Common.Models;

namespace K7.Server.Application.Common.Interfaces;

public interface IIdentityService // How to put this into domain?
{
    Task<string?> GetUserNameAsync(string userId);

    Task<bool> IsInRoleAsync(string userId, string role);

    Task<bool> AuthorizeAsync(string userId, string policyName);

    Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password);

    Task<Result> DeleteUserAsync(string userId);
}
