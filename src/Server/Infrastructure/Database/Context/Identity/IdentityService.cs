using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

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

    public async Task<bool> HasPasswordAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        return await _userManager.HasPasswordAsync(user);
    }

    public async Task<bool> VerifyPasswordAsync(string userId, string password)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        return await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to change password: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task SetPasswordAsync(string userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var result = await _userManager.AddPasswordAsync(user, newPassword);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to set password: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task RemovePasswordAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var result = await _userManager.RemovePasswordAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to remove password: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task UpdateEmailAsync(string userId, string newEmail)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var token = await _userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        var result = await _userManager.ChangeEmailAsync(user, newEmail, token);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to update email: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        // Keep username in sync with email
        await _userManager.SetUserNameAsync(user, newEmail);
    }

    public async Task<IList<Application.Common.Interfaces.ExternalLoginInfo>> GetExternalLoginsAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var logins = await _userManager.GetLoginsAsync(user);

        return logins.Select(l => new Application.Common.Interfaces.ExternalLoginInfo(
            l.LoginProvider, l.ProviderKey, l.ProviderDisplayName)).ToList();
    }

    public async Task RemoveExternalLoginAsync(string userId, string provider, string providerKey)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var result = await _userManager.RemoveLoginAsync(user, provider, providerKey);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to remove external login: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task AddExternalLoginAsync(string userId, string provider, string providerKey, string displayName)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var login = new UserLoginInfo(provider, providerKey, displayName);
        var result = await _userManager.AddLoginAsync(user, login);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to add external login: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task<TwoFactorStatus> GetTwoFactorStatusAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var recoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);
        var key = await _userManager.GetAuthenticatorKeyAsync(user);

        return new TwoFactorStatus(
            await _userManager.GetTwoFactorEnabledAsync(user),
            !string.IsNullOrEmpty(key),
            recoveryCodesLeft);
    }

    public async Task<TwoFactorSetup> BeginTwoFactorSetupAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            throw new InvalidOperationException("Two-factor authentication is already enabled.");
        }

        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = await _userManager.GetEmailAsync(user)
            ?? await _userManager.GetUserNameAsync(user)
            ?? userId;

        return new TwoFactorSetup(
            FormatAuthenticatorKey(unformattedKey!),
            GenerateAuthenticatorUri(email, unformattedKey!));
    }

    public async Task<IReadOnlyList<string>> VerifyAndEnableTwoFactorAsync(string userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var verificationCode = code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            verificationCode);

        if (!isValid)
        {
            throw new InvalidOperationException("Invalid verification code.");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);

        if (await _userManager.CountRecoveryCodesAsync(user) > 0)
            return [];

        return (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToList() ?? [];
    }

    public async Task<IReadOnlyList<string>> GenerateRecoveryCodesAsync(string userId, int count = 10)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
        {
            throw new InvalidOperationException("Two-factor authentication is not enabled.");
        }

        return (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, count))?.ToList() ?? [];
    }

    public async Task DisableTwoFactorAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException(userId, "Identity user");

        var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to disable two-factor authentication: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    private static string FormatAuthenticatorKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
            result.Append(unformattedKey.AsSpan(currentPosition));

        return result.ToString().ToLowerInvariant();
    }

    private static string GenerateAuthenticatorUri(string email, string unformattedKey)
    {
        const string authenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";
        return string.Format(
            CultureInfo.InvariantCulture,
            authenticatorUriFormat,
            UrlEncoder.Default.Encode("K7"),
            UrlEncoder.Default.Encode(email),
            unformattedKey);
    }
}
