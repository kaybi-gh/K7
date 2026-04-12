using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class MergeUserDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

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

    private void Cancel() => MudDialog.Cancel();

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
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
