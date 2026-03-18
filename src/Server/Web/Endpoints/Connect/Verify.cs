using System.Security.Claims;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace K7.Server.Web.Endpoints.Connect;

public class Verify : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods("/connect/verify", [HttpMethods.Get, HttpMethods.Post], async (HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IOpenIddictScopeManager scopeManager) =>
        {
            var request = context.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            // On GET: redirect to /link with user_code pre-filled (verification_uri_complete flow).
            if (HttpMethods.IsGet(context.Request.Method))
            {
                return string.IsNullOrEmpty(request.UserCode)
                    ? Results.Redirect("/link")
                    : Results.Redirect($"/link?user_code={Uri.EscapeDataString(request.UserCode)}");
            }

            // On POST: approve the device authorization.
            var cookieResult = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (cookieResult is null || !cookieResult.Succeeded)
            {
                return Results.Challenge(new AuthenticationProperties
                {
                    RedirectUri = !string.IsNullOrEmpty(request.UserCode)
                        ? $"/link?user_code={Uri.EscapeDataString(request.UserCode)}"
                        : "/link"
                });
            }

            // Validate the user_code with OpenIddict.
            var oidcResult = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (oidcResult is not { Succeeded: true }
                || string.IsNullOrEmpty(oidcResult.Principal?.GetClaim(Claims.ClientId)))
            {
                return Results.Redirect("/link?error=invalid_code");
            }

            var user = await userManager.GetUserAsync(cookieResult.Principal) ??
                throw new InvalidOperationException("The user details cannot be retrieved.");

            if (!await signInManager.CanSignInAsync(user))
            {
                return Results.Redirect("/link?error=access_denied");
            }

            var identity = new ClaimsIdentity(
                TokenValidationParameters.DefaultAuthenticationType,
                Claims.Name,
                Claims.Role);

            identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user))
                    .SetClaim(Claims.Email, await userManager.GetEmailAsync(user))
                    .SetClaim(Claims.Name, await userManager.GetUserNameAsync(user))
                    .SetClaim(Claims.PreferredUsername, await userManager.GetUserNameAsync(user))
                    .SetClaims(Claims.Role, [.. (await userManager.GetRolesAsync(user))]);

            identity.SetScopes(oidcResult.Principal.GetScopes());
            identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());
            identity.SetDestinations(GetDestinations);

            return Results.SignIn(new ClaimsPrincipal(identity),
                new AuthenticationProperties { RedirectUri = "/link?approved=true" },
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        })
        .DisableAntiforgery()
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Name or Claims.PreferredUsername:
                yield return Destinations.AccessToken;
                if (claim.Subject!.HasScope(Scopes.Profile))
                    yield return Destinations.IdentityToken;
                yield break;

            case Claims.Email:
                yield return Destinations.AccessToken;
                if (claim.Subject!.HasScope(Scopes.Email))
                    yield return Destinations.IdentityToken;
                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;
                if (claim.Subject!.HasScope(Scopes.Roles))
                    yield return Destinations.IdentityToken;
                yield break;

            case "AspNet.Identity.SecurityStamp":
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}
