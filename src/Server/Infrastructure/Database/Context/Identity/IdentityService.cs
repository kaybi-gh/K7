using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Infrastructure.Database.Context.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _userClaimsPrincipalFactory;
    private readonly IAuthorizationService _authorizationService;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        IUserClaimsPrincipalFactory<ApplicationUser> userClaimsPrincipalFactory,
        IAuthorizationService authorizationService)
    {
        _userManager = userManager;
        _userClaimsPrincipalFactory = userClaimsPrincipalFactory;
        _authorizationService = authorizationService;
    }

    public async Task<string?> GetUserNameAsync(string userId)
    {
        var user = await _userManager.Users.FirstAsync(u => u.Id == userId);

        return user.UserName;
    }

    public async Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName.Contains('@') ? userName : $"{userName}@local",
        };

        var result = await _userManager.CreateAsync(user, password);

        return (result.ToApplicationResult(), user.Id);
    }

    public async Task<bool> IsInRoleAsync(string userId, string role)
    {
        var user = _userManager.Users.SingleOrDefault(u => u.Id == userId);

        return user != null && await _userManager.IsInRoleAsync(user, role);
    }

    public async Task<bool> AuthorizeAsync(string userId, string policyName)
    {
        var user = _userManager.Users.SingleOrDefault(u => u.Id == userId);

        if (user == null)
        {
            return false;
        }

        var principal = await _userClaimsPrincipalFactory.CreateAsync(user);

        var result = await _authorizationService.AuthorizeAsync(principal, policyName);

        return result.Succeeded;
    }

    public async Task<Result> DeleteUserAsync(string userId)
    {
        var user = _userManager.Users.SingleOrDefault(u => u.Id == userId);

        return user != null ? await DeleteUserAsync(user) : Result.Success();
    }

    public async Task<Result> DeleteUserAsync(ApplicationUser user)
    {
        var result = await _userManager.DeleteAsync(user);

        return result.ToApplicationResult();
    }

    public async Task<string?> GetEmailAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user?.Email;
    }

    public async Task<IList<string>> GetRolesAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user is not null ? await _userManager.GetRolesAsync(user) : [];
    }

    public async Task SetRoleAsync(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

        await _userManager.AddToRoleAsync(user, role);
    }

    public async Task ResetPasswordAsync(string userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to reset password: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}
