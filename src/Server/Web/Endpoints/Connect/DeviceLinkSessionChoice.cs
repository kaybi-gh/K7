using K7.Server.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.SharedProfiles.Queries.FilterDeviceLinkSharedProfileIds;
using K7.Server.Application.Features.Users.Commands.VerifyUserPin;
using K7.Server.Application.Features.Users.Queries.GetGuardianSessionTarget;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace K7.Server.Web.Endpoints.Connect;

/// <summary>
/// Account and shared-profile choice shared by the device-code page and the browser sign-in.
/// </summary>
public static class DeviceLinkSessionChoice
{
    public const string TargetUserIdParameter = "target_user_id";
    public const string PinParameter = "pin";
    public const string SharedProfileIdParameter = "shared_profile_id";
    public const string ChoiceParameter = "k7_choice";
    public const string AccessDeniedError = "access_denied";
    public const string InvalidPinError = "invalid_pin";

    public static bool IsChoiceSubmitted(HttpContext context)
    {
        if (context.Request.HasFormContentType
            && context.Request.Form.TryGetValue(ChoiceParameter, out var posted)
            && string.Equals(posted.ToString(), "1", StringComparison.Ordinal))
            return true;

        var request = context.GetOpenIddictServerRequest();
        return string.Equals((string?)request?[ChoiceParameter], "1", StringComparison.Ordinal);
    }

    public static async Task<bool> HasChoiceAsync(
        IApplicationDbContext context,
        string identityUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(identityUserId))
            return false;

        var domainUserId = await context.Users
            .AsNoTracking()
            .Where(user => user.IdentityUserId == identityUserId)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (domainUserId is null)
            return false;

        var manages = await context.UserGuardians
            .AsNoTracking()
            .AnyAsync(link => link.GuardianUserId == domainUserId
                              && link.User.IsActive
                              && link.User.IdentityUserId != null
                              && link.User.PeerServerId == null, cancellationToken);

        if (manages)
            return true;

        return await context.SharedProfiles
            .AsNoTracking()
            .AnyAsync(group => group.HostUserId == domainUserId
                               || group.Members.Any(member => member.UserId == domainUserId), cancellationToken);
    }

    public static async Task<(ApplicationUser? Account, string? Error)> ResolveManagedAccountAsync(
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

        if (target.HasPin
            && (pin is not { Length: 4 }
                || !pin.All(char.IsAsciiDigit)
                || !await sender.Send(new VerifyUserPinCommand(target.UserId, pin), cancellationToken)))
        {
            return (null, InvalidPinError);
        }

        return (await userManager.FindByIdAsync(target.IdentityUserId), null);
    }

    public static async Task<IReadOnlyList<Guid>> FilterPinnedProfilesAsync(
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

    public static string ChoicePageUrl(OpenIddictRequest request, string? error, Guid? targetUserId)
    {
        var items = new List<KeyValuePair<string, string?>>();
        foreach (var (key, value) in request.GetParameters())
        {
            if (IsLocalField(key))
                continue;

            var text = value.ToString();
            if (string.IsNullOrEmpty(text))
                continue;

            items.Add(new KeyValuePair<string, string?>(key, text));
        }

        if (!string.IsNullOrEmpty(error))
            items.Add(new KeyValuePair<string, string?>("error", error));

        if (targetUserId is Guid id)
            items.Add(new KeyValuePair<string, string?>(TargetUserIdParameter, id.ToString("D")));

        return "/link-app" + QueryString.Create(items);
    }

    private static bool IsLocalField(string key) =>
        key.Equals(TargetUserIdParameter, StringComparison.OrdinalIgnoreCase)
        || key.Equals(PinParameter, StringComparison.OrdinalIgnoreCase)
        || key.Equals(SharedProfileIdParameter, StringComparison.OrdinalIgnoreCase)
        || key.Equals(ChoiceParameter, StringComparison.OrdinalIgnoreCase)
        || key.Equals("prompt", StringComparison.OrdinalIgnoreCase)
        || key.Equals("error", StringComparison.OrdinalIgnoreCase);
}
