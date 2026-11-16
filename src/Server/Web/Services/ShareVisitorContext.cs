using System.Security.Claims;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Web.Infrastructure;

namespace K7.Server.Web.Services;

/// <summary>
/// Reads the share grants of the current request from its principal.
/// </summary>
public class ShareVisitorContext(IHttpContextAccessor httpContextAccessor) : IShareVisitorContext
{
    /// <summary>
    /// A request that also carries a real account keeps the rights of that account:
    /// a signed-in user previewing a share link is not restricted to the share.
    /// </summary>
    public bool IsShareVisitor
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            return principal is not null
                && principal.IsInRole(Roles.ShareVisitor)
                && !principal.IsInRole(Roles.Guest)
                && !principal.IsInRole(Roles.User)
                && !principal.IsInRole(Roles.Administrator);
        }
    }

    public IReadOnlyList<ShareVisitorGrant> Grants => field ??= ReadGrants();

    private IReadOnlyList<ShareVisitorGrant> ReadGrants()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal is null)
            return [];

        var grants = new List<ShareVisitorGrant>();
        foreach (var claim in principal.FindAll(ShareSessionDefaults.GrantClaimType))
        {
            var parts = claim.Value.Split(':');
            if (parts.Length == 2
                && Guid.TryParse(parts[0], out var shareId)
                && Guid.TryParse(parts[1], out var sessionId))
            {
                grants.Add(new ShareVisitorGrant(shareId, sessionId));
            }
        }

        return grants;
    }
}
