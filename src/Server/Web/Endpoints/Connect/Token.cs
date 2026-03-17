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

public class Token : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/connect/token", async (HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager) =>
        {
            var request = context.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType() || request.IsDeviceCodeGrantType())
            {
                // Récupère les infos stockées dans le token (code d'autorisation ou refresh token)
                var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                if (result.Principal is null)
                {
                    return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Invalid token."
                    }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
                }

                // Récupère l'utilisateur correspondant au token
                var user = await userManager.FindByIdAsync(result.Principal.GetClaim(Claims.Subject)!);
                if (user is null)
                {
                    return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                    }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
                }

                // Vérifie si l'utilisateur peut toujours se connecter
                if (!await signInManager.CanSignInAsync(user))
                {
                    return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is no longer allowed to sign in."
                    }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
                }

                // Crée une nouvelle identité avec les dernières informations utilisateur
                var identity = new ClaimsIdentity(result.Principal.Claims, TokenValidationParameters.DefaultAuthenticationType,
                    Claims.Name, Claims.Role);

                identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user))
                        .SetClaim(Claims.Email, await userManager.GetEmailAsync(user) ?? "")
                        .SetClaim(Claims.Name, await userManager.GetUserNameAsync(user) ?? "")
                        .SetClaim(Claims.PreferredUsername, await userManager.GetUserNameAsync(user) ?? "")
                        .SetClaims(Claims.Role, [.. (await userManager.GetRolesAsync(user))]);

                identity.SetDestinations(GetDestinations);

                return Results.SignIn(new ClaimsPrincipal(identity),
                    authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return Results.BadRequest("Unsupported grant type.");
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        // Note: by default, claims are NOT automatically included in the access and identity tokens.
        // To allow OpenIddict to serialize them, you must attach them a destination, that specifies
        // whether they should be included in access tokens, in identity tokens or in both.

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

            // Never include the security stamp in the access and identity tokens, as it's a secret value.
            case "AspNet.Identity.SecurityStamp": yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}
