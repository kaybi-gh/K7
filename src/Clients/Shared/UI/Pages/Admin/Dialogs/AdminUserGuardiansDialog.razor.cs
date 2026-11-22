using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

/// <summary>
/// Picks the guardians of an account. Closes with the guardian ids, empty when the account is
/// not managed. There is no stored "managed" flag: the switch only reveals the list.
/// </summary>
public partial class AdminUserGuardiansDialog
{
    private bool _managed;
    private HashSet<Guid> _guardianIds = [];

    // Current guardians and every account ticked or unticked since: always listed.
    private HashSet<Guid> _pinnedIds = [];
    private List<UserDto> _candidates = [];
    private string _search = "";
    private string? _validationError;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    /// <summary>The account whose guardians are edited.</summary>
    [Parameter, EditorRequired] public UserDto User { get; set; } = null!;

    [Parameter] public IReadOnlyList<UserDto> AllUsers { get; set; } = [];

    // A guardian gets a full session of the account, so an administrator cannot be managed.
    private bool IsAdministrator => User.Role == Roles.Administrator;

    protected override void OnInitialized()
    {
        _guardianIds = [.. User.GuardianUserIds];
        _pinnedIds = [.. User.GuardianUserIds];
        _managed = !IsAdministrator && _guardianIds.Count > 0;
        _candidates = GuardianCandidates.GetEligible(User, AllUsers);
    }

    private static string GetLetter(UserDto user) =>
        GuardianCandidates.GetName(user)[..1].ToUpperInvariant();

    private void OnManagedChanged(bool managed)
    {
        _managed = managed;
        _validationError = null;
    }

    private void ToggleGuardian(Guid guardianUserId, bool selected)
    {
        if (selected)
            _guardianIds.Add(guardianUserId);
        else
            _guardianIds.Remove(guardianUserId);

        _pinnedIds.Add(guardianUserId);
        _validationError = null;
    }

    private void Cancel() => Dialog.Cancel();

    private void Submit()
    {
        if (!_managed)
        {
            Dialog.Close(K7DialogResult.Ok(new List<Guid>()));
            return;
        }

        if (_guardianIds.Count == 0)
        {
            _validationError = L["GuardianRequired"];
            return;
        }

        Dialog.Close(K7DialogResult.Ok(_guardianIds.ToList()));
    }
}
