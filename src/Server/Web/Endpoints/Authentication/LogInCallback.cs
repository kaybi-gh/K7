using System.Security.Claims;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Configuration;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client.AspNetCore;
using Microsoft.EntityFrameworkCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace K7.Server.Web.Endpoints.Authentication;

public class LogInCallback : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods("/api/authentication/callback/login/{provider}", [HttpMethods.Get, HttpMethods.Post],
            async (HttpContext context,
                   [FromServices] UserManager<ApplicationUser> userManager,
                   [FromServices] IApplicationDbContext applicationDbContext,
                   [FromServices] IOptions<AuthenticationConfiguration> authConfig,
                   [FromRoute] string provider,
                   CancellationToken cancellationToken) =>
        {
            // Retrieve the authorization data validated by OpenIddict as part of the callback handling.
            var result = await context.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);

            // Multiple strategies exist to handle OAuth 2.0/OpenID Connect callbacks, each with their pros and cons:
            //
            //   * Directly using the tokens to perform the necessary action(s) on behalf of the user, which is suitable
            //     for applications that don't need a long-term access to the user's resources or don't want to store
            //     access/refresh tokens in a database or in an authentication cookie (which has security implications).
            //     It is also suitable for applications that don't need to authenticate users but only need to perform
            //     action(s) on their behalf by making API calls using the access token returned by the remote server.
            //
            //   * Storing the external claims/tokens in a database (and optionally keeping the essential claims in an
            //     authentication cookie so that cookie size limits are not hit). For the applications that use ASP.NET
            //     Core Identity, the UserManager.SetAuthenticationTokenAsync() API can be used to store external tokens.
            //
            //     Note: in this case, it's recommended to use column encryption to protect the tokens in the database.
            //
            //   * Storing the external claims/tokens in an authentication cookie, which doesn't require having
            //     a user database but may be affected by the cookie size limits enforced by most browser vendors
            //     (e.g Safari for macOS and Safari for iOS/iPadOS enforce a per-domain 4KB limit for all cookies).
            //
            //     Note: this is the approach used here, but the external claims are first filtered to only persist
            //     a few claims like the user identifier. The same approach is used to store the access/refresh tokens.

            // Important: if the remote server doesn't support OpenID Connect and doesn't expose a userinfo endpoint,
            // result.Principal.Identity will represent an unauthenticated identity and won't contain any user claim.
            //
            // Such identities cannot be used as-is to build an authentication cookie in ASP.NET Core (as the
            // antiforgery stack requires at least a name claim to bind CSRF cookies to the user's identity) but
            // the access/refresh tokens can be retrieved using result.Properties.GetTokens() to make API calls.
            if (result.Principal is not ClaimsPrincipal { Identity.IsAuthenticated: true })
            {
                throw new InvalidOperationException("The external authorization data cannot be used for authentication.");
            }

            // Extract the provider's user identifier from the claims
            var providerKey = result.Principal.GetClaim(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Provider key (NameIdentifier) is missing.");

            // Extract the email from the claims (if provided by the external provider)
            var email = result.Principal.GetClaim(ClaimTypes.Email)
                ?? throw new InvalidOperationException("Email claim is missing.");

            var name = result.Principal.GetClaim(ClaimTypes.Name);

            // Check if the user already exists in the local database
            var user = await userManager.FindByLoginAsync(provider, providerKey);
            if (user == null)
            {
                if (!authConfig.Value.Oidc.AutomaticAccountCreation)
                {
                    return Results.Redirect("/sign-in?error=auto_provisioning_disabled");
                }

                user = new ApplicationUser { UserName = name, Email = email };

                var creationResult = await userManager.CreateAsync(user);
                if (!creationResult.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to create user: {string.Join(", ", creationResult.Errors.Select(e => e.Description))}");
                }

                await userManager.AddToRoleAsync(user, Roles.User);

                // Associate the external LogIn (provider) with the new local user
                var LogInInfo = new UserLoginInfo(provider, providerKey, provider);
                var addLogInResult = await userManager.AddLoginAsync(user, LogInInfo);
                if (!addLogInResult.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to associate LogIn: {string.Join(", ", addLogInResult.Errors.Select(e => e.Description))}");
                }
            }

            // Ensure SSO user has at least the User role
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Count == 0)
            {
                await userManager.AddToRoleAsync(user, Roles.User);
            }

            // Ensure a corresponding domain user exists for this identity user
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

            ///

            // Build an identity based on the external claims and that will be used to create the authentication cookie.
            var identity = new ClaimsIdentity(
                authenticationType: "ExternalLogIn",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);

            // By default, OpenIddict will automatically try to map the email/name and name identifier claims from
            // their standard OpenID Connect or provider-specific equivalent, if available. If needed, additional
            // claims can be resolved from the external identity and copied to the final authentication cookie.
            identity.SetClaim(ClaimTypes.Email, result.Principal.GetClaim(ClaimTypes.Email))
                    .SetClaim(ClaimTypes.Name, result.Principal.GetClaim(ClaimTypes.Name))
                    .SetClaim(ClaimTypes.NameIdentifier, result.Principal.GetClaim(ClaimTypes.NameIdentifier));

            // Preserve the registration details to be able to resolve them later.
            identity.SetClaim(Claims.Private.RegistrationId, result.Principal.GetClaim(Claims.Private.RegistrationId))
                    .SetClaim(Claims.Private.ProviderName, result.Principal.GetClaim(Claims.Private.ProviderName));

            // Build the authentication properties based on the properties that were added when the challenge was triggered.
            var properties = new AuthenticationProperties(result.Properties!.Items)
            {
                RedirectUri = result.Properties.RedirectUri ?? "/"
            };

            // If needed, the tokens returned by the authorization server can be stored in the authentication cookie.
            // To make cookies less heavy, tokens that are not used are filtered out before creating the cookie.
            properties.StoreTokens(result.Properties.GetTokens().Where(token => token.Name is
                // Preserve the access and refresh tokens returned in the token response, if available.
                OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken or
                OpenIddictClientAspNetCoreConstants.Tokens.RefreshToken));

            // Ask the default sign-in handler to return a new cookie and redirect the
            // user agent to the return URL stored in the authentication properties.
            //
            // For scenarios where the default sign-in handler configured in the ASP.NET Core
            // authentication options shouldn't be used, a specific scheme can be specified here.
            return Results.SignIn(new ClaimsPrincipal(identity), properties, IdentityConstants.ExternalScheme);
        })
        //.RequireAuthorization();
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
