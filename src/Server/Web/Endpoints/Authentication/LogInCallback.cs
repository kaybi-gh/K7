using System.Security.Claims;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Configuration;
using K7.Server.Infrastructure.Database.Context.Identity;
using K7.Server.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client.AspNetCore;

namespace K7.Server.Web.Endpoints.Authentication;

public class LogInCallback : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods("/api/authentication/callback/login/{provider}", [HttpMethods.Get, HttpMethods.Post],
            async (HttpContext context,
                   [FromServices] UserManager<ApplicationUser> userManager,
                   [FromServices] SignInManager<ApplicationUser> signInManager,
                   [FromServices] IApplicationDbContext applicationDbContext,
                   [FromServices] ISetupService setupService,
                   [FromServices] IOptions<AuthenticationConfiguration> authConfig,
                   [FromRoute] string provider,
                   CancellationToken cancellationToken) =>
            {
                var result = await context.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);

                if (result.Principal is not ClaimsPrincipal { Identity.IsAuthenticated: true })
                {
                    throw new InvalidOperationException("The external authorization data cannot be used for authentication.");
                }

                var providerKey = result.Principal.GetClaim(ClaimTypes.NameIdentifier)
                    ?? throw new InvalidOperationException("Provider key (NameIdentifier) is missing.");

                var email = result.Principal.GetClaim(ClaimTypes.Email);
                var name = result.Principal.GetClaim(ClaimTypes.Name);
                var preferredUsername = result.Principal.GetClaim("preferred_username");
                var userName = preferredUsername ?? name ?? email ?? providerKey;

                var returnUrl = result.Properties!.RedirectUri ?? "/";
                if (!await setupService.IsSetupCompletedAsync(cancellationToken)
                    && IsSetupExternalLoginReturnUrl(returnUrl))
                {
                    var properties = result.Properties;
                    properties.Items["LoginProvider"] = provider;
                    await context.SignInAsync(IdentityConstants.ExternalScheme, result.Principal, properties);
                    return Results.Redirect(returnUrl);
                }

                if (OidcLinkHelper.IsLinkRequest(result.Properties?.Items, returnUrl))
                {
                    return await LinkExternalLoginAsync(
                        context, userManager, provider, providerKey, returnUrl);
                }

                var user = await userManager.FindByLoginAsync(provider, providerKey);
                if (user == null)
                {
                    if (!authConfig.Value.Oidc.AutomaticAccountCreation)
                    {
                        return Results.Redirect("/sign-in?error=auto_provisioning_disabled");
                    }

                    // Never auto-link by email: an unknown IdP subject always gets a new account
                    // (or fails). Linking an existing local account must be an explicit user action.
                    user = new ApplicationUser
                    {
                        UserName = userName,
                        Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    };

                    var creationResult = await userManager.CreateAsync(user);
                    if (!creationResult.Succeeded)
                    {
                        if (creationResult.Errors.Any(e =>
                                e.Code is "DuplicateEmail" or "DuplicateUserName"))
                        {
                            return Results.Redirect("/sign-in?error=account_exists");
                        }

                        throw new InvalidOperationException($"Failed to create user: {string.Join(", ", creationResult.Errors.Select(e => e.Description))}");
                    }

                    await userManager.AddToRoleAsync(user, Roles.User);

                    var loginInfo = new UserLoginInfo(provider, providerKey, provider);
                    var addLoginResult = await userManager.AddLoginAsync(user, loginInfo);
                    if (!addLoginResult.Succeeded)
                    {
                        throw new InvalidOperationException($"Failed to associate LogIn: {string.Join(", ", addLoginResult.Errors.Select(e => e.Description))}");
                    }
                }

                var roles = await userManager.GetRolesAsync(user);
                if (roles.Count == 0)
                {
                    await userManager.AddToRoleAsync(user, Roles.User);
                }

                var identityUserId = user.Id;
                var domainUser = await applicationDbContext.Users
                    .SingleOrDefaultAsync(u => u.IdentityUserId == identityUserId, cancellationToken);

                if (domainUser is null)
                {
                    domainUser = new User
                    {
                        IdentityUserId = identityUserId,
                        DisplayName = name
                    };

                    applicationDbContext.Users.Add(domainUser);
                    await applicationDbContext.SaveChangesAsync(cancellationToken);
                }

                // Mark the upcoming application cookie as an OIDC session so OnSigningIn
                // applies Authentication:Oidc:WebSessionLifetime (default 7 days).
                context.Items[ApplicationCookieOptions.OidcSessionMarkerKey] =
                    ApplicationCookieOptions.OidcSessionMarkerValue;

                // Persistent cookie survives browser restart. Lifetime is local K7 config
                // (Oidc:WebSessionLifetime), not continuous IdP refresh.
                var signInResult = await signInManager.ExternalLoginSignInAsync(
                    provider,
                    providerKey,
                    isPersistent: true,
                    bypassTwoFactor: true);

                if (signInResult.Succeeded)
                {
                    return Results.Redirect(returnUrl);
                }

                if (signInResult.IsLockedOut)
                {
                    return Results.Redirect("/sign-in/lockout");
                }

                throw new InvalidOperationException("Unable to sign in user after external login.");
            })
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static async Task<IResult> LinkExternalLoginAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        string provider,
        string providerKey,
        string returnUrl)
    {
        var appAuth = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!appAuth.Succeeded || appAuth.Principal is null)
            return Results.Redirect(OidcLinkHelper.BuildResultUrl(returnUrl, "error"));

        var currentIdentityId = appAuth.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentIdentityId))
            return Results.Redirect(OidcLinkHelper.BuildResultUrl(returnUrl, "error"));

        var currentUser = await userManager.FindByIdAsync(currentIdentityId);
        if (currentUser is null)
            return Results.Redirect(OidcLinkHelper.BuildResultUrl(returnUrl, "error"));

        var existingLoginOwner = await userManager.FindByLoginAsync(provider, providerKey);
        if (existingLoginOwner is not null)
        {
            if (string.Equals(existingLoginOwner.Id, currentUser.Id, StringComparison.Ordinal))
                return Results.Redirect(OidcLinkHelper.BuildResultUrl(returnUrl, "already_linked"));

            return Results.Redirect(OidcLinkHelper.BuildResultUrl(returnUrl, "conflict"));
        }

        var loginInfo = new UserLoginInfo(provider, providerKey, provider);
        var addLoginResult = await userManager.AddLoginAsync(currentUser, loginInfo);
        if (!addLoginResult.Succeeded)
            return Results.Redirect(OidcLinkHelper.BuildResultUrl(returnUrl, "error"));

        return Results.Redirect(OidcLinkHelper.BuildResultUrl(returnUrl, "success"));
    }

    private static bool IsSetupExternalLoginReturnUrl(string returnUrl) =>
        returnUrl.Contains("/setup/external-login", StringComparison.OrdinalIgnoreCase);
}
