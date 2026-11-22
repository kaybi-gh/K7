using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Users;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Accounts offered as guardians of an account, and the part of that list shown at once.
/// </summary>
public static class GuardianCandidates
{
    /// <summary>Accounts listed besides the pinned ones. The search reaches the others.</summary>
    public const int MaxListed = 8;

    /// <summary>
    /// Accounts that can be guardians of <paramref name="account"/>: current guardians first, then by name.
    /// </summary>
    public static List<UserDto> GetEligible(UserDto account, IReadOnlyList<UserDto> allUsers)
    {
        var guardianIds = account.GuardianUserIds.ToHashSet();

        return allUsers
            .Where(candidate => candidate.Id != account.Id
                && candidate.IdentityUserId is not null
                && candidate.Role is Roles.Administrator or Roles.User
                // An inactive account cannot sign in. Keep it listed only while it is still a guardian.
                && (candidate.IsActive || guardianIds.Contains(candidate.Id)))
            .OrderByDescending(candidate => guardianIds.Contains(candidate.Id))
            .ThenBy(GetName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The candidates to list for a search. Pinned accounts (current guardians and accounts ticked
    /// or unticked since) are always listed when they match, so a row never vanishes under the pointer.
    /// The other matches stop at <paramref name="maxListed"/>: <c>HiddenCount</c> is how many were left out.
    /// </summary>
    public static (List<UserDto> Items, int HiddenCount) GetVisible(
        IReadOnlyList<UserDto> candidates,
        string? search,
        IReadOnlySet<Guid> pinnedIds,
        int maxListed = MaxListed)
    {
        var term = search?.Trim() ?? "";
        var items = new List<UserDto>();
        var listed = 0;
        var hidden = 0;

        foreach (var candidate in candidates)
        {
            if (term.Length > 0 && !Matches(candidate, term))
                continue;

            if (pinnedIds.Contains(candidate.Id))
            {
                items.Add(candidate);
                continue;
            }

            if (listed < maxListed)
            {
                items.Add(candidate);
                listed++;
            }
            else
            {
                hidden++;
            }
        }

        return (items, hidden);
    }

    public static string GetName(UserDto user) =>
        user.DisplayName ?? user.UserName ?? user.Email ?? user.Id.ToString();

    private static bool Matches(UserDto user, string term) =>
        Contains(user.DisplayName, term) || Contains(user.UserName, term) || Contains(user.Email, term);

    private static bool Contains(string? value, string term) =>
        value?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false;
}
