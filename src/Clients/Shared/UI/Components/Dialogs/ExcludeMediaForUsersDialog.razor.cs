using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class ExcludeMediaForUsersDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;
    [Parameter] public Guid MediaId { get; set; }
    [Parameter] public string? MediaTitle { get; set; }

    private bool _loading = true;
    private bool _saving;
    private List<UserExclusionState> _userStates = [];
    private string _search = "";

    private List<UserExclusionState> FilteredStates
    {
        get
        {
            var search = _search.Trim();
            if (search.Length == 0)
                return _userStates;

            return _userStates
                .Where(s => GetName(s.User).Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (s.User.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }
    }

    private int HiddenCount => _userStates.Count(s => s.Excluded);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var users = await K7ServerService.GetUsersAsync();
            _userStates = users
                .OrderBy(u => u.UserName ?? u.Email, StringComparer.CurrentCultureIgnoreCase)
                .Select(u => new UserExclusionState
                {
                    User = u,
                    Excluded = u.MediaExclusions.Any(e => e.MediaId == MediaId && e.IsAdminExcluded),
                    OriginalExcluded = u.MediaExclusions.Any(e => e.MediaId == MediaId && e.IsAdminExcluded)
                }).ToList();
        }
        catch
        {
            _userStates = [];
        }
        _loading = false;
    }

    private static string GetName(UserDto user) =>
        !string.IsNullOrEmpty(user.UserName) ? user.UserName
        : !string.IsNullOrEmpty(user.Email) ? user.Email
        : "-";

    private void Cancel() => Dialog.Cancel();

    private async Task Submit()
    {
        _saving = true;
        try
        {
            var modified = _userStates.Where(s => s.Excluded != s.OriginalExcluded).ToList();
            foreach (var entry in modified)
            {
                var newExclusions = entry.User.MediaExclusions
                    .Where(e => e.IsAdminExcluded)
                    .Select(e => e.MediaId)
                    .ToList();
                if (entry.Excluded)
                    newExclusions.Add(MediaId);
                else
                    newExclusions.Remove(MediaId);

                await K7ServerService.UpdateUserMediaExclusionsAsync(entry.User.Id, new UpdateUserMediaExclusionsRequest
                {
                    ExcludedMediaIds = newExclusions
                });
            }
            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private sealed class UserExclusionState
    {
        public required UserDto User { get; init; }
        public bool Excluded { get; set; }
        public bool OriginalExcluded { get; init; }
    }
}
