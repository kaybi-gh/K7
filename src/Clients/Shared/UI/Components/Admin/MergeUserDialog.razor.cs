using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class MergeUserDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public required UserDto SourceUser { get; set; }
    [Parameter] public required List<UserDto> AllUsers { get; set; }

    private string SourceDisplayName => SourceUser.UserName ?? SourceUser.Email ?? SourceUser.Id.ToString();

    private List<UserDto> _availableTargets = [];
    private Guid? _targetUserId;
    private bool _isSubmitting;
    private PlayCountMergeMode _playCountMode = PlayCountMergeMode.Additive;
    private RatingConflictMode _ratingMode = RatingConflictMode.KeepExisting;
    private ProgressConflictMode _progressMode = ProgressConflictMode.MostRecent;

    protected override void OnParametersSet()
    {
        _availableTargets = AllUsers
            .Where(u => u.Id != SourceUser.Id && !u.IsGuest)
            .ToList();
    }

    private void Cancel() => Dialog.Cancel();

    private async Task Submit()
    {
        if (!_targetUserId.HasValue) return;

        _isSubmitting = true;
        try
        {
            var strategy = new MergeStrategy
            {
                PlayCount = _playCountMode,
                Rating = _ratingMode,
                Progress = _progressMode
            };
            await K7ServerService.MergeUsersAsync(SourceUser.Id, _targetUserId.Value, strategy);
            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
