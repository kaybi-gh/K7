using K7.Clients.Shared.Models;
using K7.Shared.Dtos.SharedProfiles;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class LeaveSharedProfileDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;
    [Parameter] public required IReadOnlyList<SharedProfileMemberDto> OtherMembers { get; set; }

    private Guid _newHostUserId;

    protected override void OnParametersSet()
    {
        _newHostUserId = OtherMembers.FirstOrDefault()?.UserId ?? Guid.Empty;
    }

    private string FormatMember(Guid userId) =>
        OtherMembers.FirstOrDefault(m => m.UserId == userId)?.DisplayName ?? userId.ToString();

    private void Cancel() => Dialog.Close(K7DialogResult.Cancel());

    private void Confirm() => Dialog.Close(K7DialogResult.Ok(_newHostUserId));
}
