using System.Security.Claims;
using K7.Server.Infrastructure.Database.Context.Identity;
using MediatR;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace K7.Server.Web.Endpoints.Connect;

public class UserInfo : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods("/connect/userinfo", [HttpMethods.Get, HttpMethods.Post], async (HttpContext httpContext,
            [FromServices] UserManager<ApplicationUser> userManager) =>
        {
            var authenticateResult = await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
            {
                return Results.Challenge(
                    authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The specified access token is bound to an account that no longer exists."
                    }));
            }

            var userId = authenticateResult.Principal.GetClaim(Claims.Subject);
            var user = await userManager.FindByIdAsync(userId!);
            if (user is null)
            {
                return Results.Challenge(
                    authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The specified access token is bound to an account that no longer exists."
                    }));
            }

            var claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [Claims.Subject] = await userManager.GetUserIdAsync(user)
            };

            if (authenticateResult.Principal.HasScope(Scopes.Email))
            {
                claims[Claims.Email] = await userManager.GetEmailAsync(user) ?? "";
                claims[Claims.EmailVerified] = await userManager.IsEmailConfirmedAsync(user);
            }

            if (authenticateResult.Principal.HasScope(Scopes.Phone))
            {
                claims[Claims.PhoneNumber] = await userManager.GetPhoneNumberAsync(user) ?? "";
                claims[Claims.PhoneNumberVerified] = await userManager.IsPhoneNumberConfirmedAsync(user);
            }

            if (authenticateResult.Principal.HasScope(Scopes.Roles))
            {
                claims[Claims.Role] = await userManager.GetRolesAsync(user);
            }

            return Results.Json(claims);
        })
        //.RequireAuthorization(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
