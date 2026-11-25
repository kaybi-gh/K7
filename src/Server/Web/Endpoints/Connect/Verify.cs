using System.Security.Claims;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Features.SharedProfiles.Queries.FilterDeviceLinkSharedProfileIds;
using K7.Server.Application.Features.Users.Commands.VerifyUserPin;
using K7.Server.Application.Features.Users.Queries.GetGuardianSessionTarget;
using K7.Shared;
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
    /// <summary>Form field: the managed account the session is asked for. Empty for the signed-in user.</summary>
    public const string TargetUserIdParameter = "target_user_id";

    /// <summary>Form field: the PIN of the managed account, when it has one.</summary>
    public const string PinParameter = "pin";

    /// <summary>Form field: shared profiles of the linked account to show on that device.</summary>
    public const string SharedProfileIdParameter = "shared_profile_id";

    private const string AccessDeniedError = "access_denied";
    private const string InvalidPinError = "invalid_pin";

    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods("/connect/verify", [HttpMethods.Get, HttpMethods.Post], async (HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IOpenIddictScopeManager scopeManager,
            [FromServices] ISender sender,
            [FromServices] ILogger<Verify> logger) =>
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

            // A guardian can ask the session for an account they manage instead of their own.
            // The signed-in browser user stays the one who authorizes. Only the token subject changes.
            var targetUserId = (string?)request[TargetUserIdParameter];
            if (!string.IsNullOrEmpty(targetUserId))
            {
                if (!Guid.TryParse(targetUserId, out var managedUserId))
                {
                    return Results.Redirect("/link?error=access_denied");
                }

                var (managedAccount, error) = await ResolveManagedAccountAsync(
                    user,
                    managedUserId,
                    (string?)request[PinParameter],
                    userManager,
                    sender,
                    context.RequestAborted);

                if (managedAccount is null || !await signInManager.CanSignInAsync(managedAccount))
                {
                    return Results.Redirect(LinkError(request.UserCode, error ?? AccessDeniedError, managedUserId));
                }

                logger.LogInformation(
                    "Guardian {GuardianIdentityUserId} opens a device session for managed account {ManagedIdentityUserId}",
                    user.Id,
                    managedAccount.Id);

                user = managedAccount;
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

            var pinnedProfiles = await FilterPinnedProfilesAsync(context, sender, user.Id);
            if (pinnedProfiles.Count > 0)
                identity.SetClaim(DeviceLinkClaims.SharedProfileIds, string.Join(',', pinnedProfiles));

            identity.SetDestinations(GetDestinations);

            return Results.SignIn(new ClaimsPrincipal(identity),
                new AuthenticationProperties { RedirectUri = "/link?approved=true" },
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        })
        .DisableAntiforgery()
        // The form can carry the PIN of a managed account: same throttle as the PIN check.
        .RequireRateLimiting(RateLimitingExtensions.PinVerifyPolicy)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    // The managed account to sign in, or the /link error code when the guardian may not open it.
    private static async Task<(ApplicationUser? Account, string? Error)> ResolveManagedAccountAsync(
        ApplicationUser guardian,
        Guid managedUserId,
        string? pin,
        UserManager<ApplicationUser> userManager,
        ISender sender,
        CancellationToken cancellationToken)
    {
        GuardianSessionTarget target;
        try
        {
            target = await sender.Send(new GetGuardianSessionTargetQuery(guardian.Id, managedUserId), cancellationToken);
        }
        catch (ForbiddenAccessException)
        {
            return (null, AccessDeniedError);
        }

        // The format is checked here so a mistyped PIN never fails validation inside a request:
        // failing requests are logged whole.
        if (target.HasPin
            && (pin is not { Length: 4 }
                || !pin.All(char.IsAsciiDigit)
                || !await sender.Send(new VerifyUserPinCommand(target.UserId, pin), cancellationToken)))
        {
            return (null, InvalidPinError);
        }

        return (await userManager.FindByIdAsync(target.IdentityUserId), null);
    }

    private static async Task<IReadOnlyList<Guid>> FilterPinnedProfilesAsync(
        HttpContext context,
        ISender sender,
        string identityUserId)
    {
        if (!context.Request.HasFormContentType)
            return [];

        var requested = context.Request.Form[SharedProfileIdParameter]
            .Select(value => Guid.TryParse(value, out var id) ? id : (Guid?)null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .Take(FilterDeviceLinkSharedProfileIdsQueryHandler.MaxIds)
            .ToList();

        if (requested.Count == 0)
            return [];

        return await sender.Send(
            new FilterDeviceLinkSharedProfileIdsQuery(identityUserId, requested),
            context.RequestAborted);
    }

    // Keeps the code and the chosen account in the address: the guardian retries without scanning
    // again, and the retry does not fall back to the guardian's own account.
    private static string LinkError(string? userCode, string error, Guid managedUserId)
    {
        var url = $"/link?error={error}&{TargetUserIdParameter}={managedUserId}";
        return string.IsNullOrEmpty(userCode)
            ? url
            : $"{url}&user_code={Uri.EscapeDataString(userCode)}";
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
