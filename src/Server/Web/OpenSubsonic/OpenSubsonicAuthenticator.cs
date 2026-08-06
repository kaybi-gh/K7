using System.Security.Claims;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.OpenSubsonic;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Identity;
using K7.Server.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.OpenSubsonic;

public sealed class OpenSubsonicAuthResult
{
    public ClaimsPrincipal? Principal { get; init; }
    public string Username { get; init; } = string.Empty;
    public bool CanWrite { get; init; }
    public OpenSubsonicError? Error { get; init; }

    public bool IsFailed => Error is not null;

    public static OpenSubsonicAuthResult Fail(int code, string message, string? helpUrl = null) =>
        new()
        {
            Error = new OpenSubsonicError
            {
                Code = code,
                Message = message,
                HelpUrl = helpUrl
            }
        };
}

public sealed class OpenSubsonicAuthenticator(
    IApiKeyService apiKeyService,
    IClientAppPasswordService clientAppPasswordService,
    IApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ILogger<OpenSubsonicAuthenticator> logger)
{
    public async Task<OpenSubsonicAuthResult> AuthenticateAsync(
        IQueryCollection query,
        IFormCollection? form,
        CancellationToken cancellationToken = default)
    {
        var apiKey = First(query, form, "apiKey");
        var username = First(query, form, "u");
        var password = First(query, form, "p");
        var token = First(query, form, "t");
        var salt = First(query, form, "s");

        var hasApiKey = !string.IsNullOrWhiteSpace(apiKey);
        var hasUserPass = !string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(password);
        var hasTokenSalt = !string.IsNullOrWhiteSpace(token) || !string.IsNullOrWhiteSpace(salt);

        if (hasApiKey && (hasUserPass || hasTokenSalt))
        {
            return OpenSubsonicAuthResult.Fail(
                OpenSubsonicConstants.ErrorAuthConflict,
                "Conflicting authentication parameters.");
        }

        if (hasApiKey)
        {
            var validated = await apiKeyService.ValidateKeyAsync(apiKey!, cancellationToken);
            if (validated is null)
            {
                logger.LogWarning("OpenSubsonic apiKey authentication failed");
                return OpenSubsonicAuthResult.Fail(
                    OpenSubsonicConstants.ErrorInvalidApiKey,
                    "Invalid API key.");
            }

            var role = validated.Key.Scope switch
            {
                ApiKeyScope.Admin => Roles.Administrator,
                ApiKeyScope.Write => Roles.User,
                _ => Roles.Guest
            };

            var canWrite = validated.Key.Scope is ApiKeyScope.Write or ApiKeyScope.Admin;
            var owner = await userManager.FindByIdAsync(validated.IdentityUserId);
            var ownerUsername = owner?.UserName ?? validated.Key.Name;
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, validated.IdentityUserId),
                new Claim(ClaimTypes.Name, ownerUsername),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.AuthenticationScheme);
            return new OpenSubsonicAuthResult
            {
                Principal = new ClaimsPrincipal(identity),
                Username = ownerUsername,
                CanWrite = canWrite
            };
        }

        if (hasTokenSalt)
        {
            if (string.IsNullOrWhiteSpace(username)
                || string.IsNullOrWhiteSpace(token)
                || string.IsNullOrWhiteSpace(salt))
            {
                return OpenSubsonicAuthResult.Fail(
                    OpenSubsonicConstants.ErrorWrongCredentials,
                    "Missing username, token, or salt.");
            }

            return await AuthenticateWithAppSecretAsync(
                username,
                stored => clientAppPasswordService.VerifyToken(stored, token, salt),
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
            {
                return OpenSubsonicAuthResult.Fail(
                    OpenSubsonicConstants.ErrorNotAuthenticated,
                    "Authentication required.");
            }

            return OpenSubsonicAuthResult.Fail(
                OpenSubsonicConstants.ErrorWrongCredentials,
                "Missing username or password.");
        }

        var decodedPassword = DecodePassword(password);
        return await AuthenticateWithAppSecretAsync(
            username,
            stored => clientAppPasswordService.VerifyPassword(stored, decodedPassword),
            cancellationToken);
    }

    private async Task<OpenSubsonicAuthResult> AuthenticateWithAppSecretAsync(
        string username,
        Func<string, bool> verifyStoredSecret,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            logger.LogWarning("OpenSubsonic authentication failed for unknown user");
            return OpenSubsonicAuthResult.Fail(
                OpenSubsonicConstants.ErrorWrongCredentials,
                "Wrong username or password.",
                OpenSubsonicConstants.HelpUrl);
        }

        // OpenSubsonic accepts only revocable app passwords, never the account password.
        var valid = await TryClientAppPasswordAsync(user.Id, verifyStoredSecret, cancellationToken);
        if (!valid)
        {
            logger.LogWarning("OpenSubsonic app password authentication failed for user {UserName}", username);
            return OpenSubsonicAuthResult.Fail(
                OpenSubsonicConstants.ErrorWrongCredentials,
                "Wrong username or password. Create an app password under Settings -> External clients.",
                OpenSubsonicConstants.HelpUrl);
        }

        var roles = await userManager.GetRolesAsync(user);
        var claimsList = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? username)
        };
        claimsList.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var canWriteFromRoles = roles.Contains(Roles.User) || roles.Contains(Roles.Administrator);
        var principalIdentity = new ClaimsIdentity(claimsList, IdentityConstants.ApplicationScheme);

        return new OpenSubsonicAuthResult
        {
            Principal = new ClaimsPrincipal(principalIdentity),
            Username = user.UserName ?? username,
            CanWrite = canWriteFromRoles
        };
    }

    private async Task<bool> TryClientAppPasswordAsync(
        string identityUserId,
        Func<string, bool> verifyStoredSecret,
        CancellationToken cancellationToken)
    {
        var domainUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdentityUserId == identityUserId, cancellationToken);

        if (domainUser is null)
            return false;

        var appPasswords = await dbContext.ClientAppPasswords
            .AsNoTracking()
            .Where(p => p.UserId == domainUser.Id)
            .Select(p => new { p.Id, p.PasswordHash })
            .ToListAsync(cancellationToken);

        foreach (var appPassword in appPasswords)
        {
            if (!verifyStoredSecret(appPassword.PasswordHash))
                continue;

            await dbContext.ClientAppPasswords
                .Where(p => p.Id == appPassword.Id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(p => p.LastUsedAt, DateTime.UtcNow),
                    cancellationToken);
            return true;
        }

        return false;
    }

    private static string? First(IQueryCollection query, IFormCollection? form, string key)
    {
        if (query.TryGetValue(key, out var queryValues))
        {
            var value = queryValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        if (form is not null && form.TryGetValue(key, out var formValues))
        {
            var value = formValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    private static string DecodePassword(string password)
    {
        if (!password.StartsWith("enc:", StringComparison.OrdinalIgnoreCase))
            return password;

        var hex = password[4..];
        if (hex.Length % 2 != 0)
            return password;

        try
        {
            var bytes = Convert.FromHexString(hex);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return password;
        }
    }
}
