using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class AdminLibraryUsersDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;
    [Parameter] public Guid LibraryId { get; set; }

    private bool _loading = true;
    private bool _saving;
    private List<UserDto> _users = [];
    private HashSet<Guid> _excludedUserIds = [];
    private Dictionary<Guid, List<Guid>> _originalExclusions = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _users = await K7ServerService.GetUsersAsync();
            _originalExclusions = _users.ToDictionary(u => u.Id, u => new List<Guid>(u.ExcludedLibraryIds));
            _excludedUserIds = _users
                .Where(u => u.ExcludedLibraryIds.Contains(LibraryId))
                .Select(u => u.Id)
                .ToHashSet();
        }
        catch
        {
            _users = [];
        }
        _loading = false;
    }

    private void ToggleAccess(Guid userId, bool hasAccess)
    {
        if (hasAccess)
            _excludedUserIds.Remove(userId);
        else
            _excludedUserIds.Add(userId);
    }

    private void Cancel() => Dialog.Cancel();

    private async Task Submit()
    {
        _saving = true;
        try
        {
            foreach (var user in _users)
            {
                var wasExcluded = _originalExclusions[user.Id].Contains(LibraryId);
                var isExcluded = _excludedUserIds.Contains(user.Id);

                if (wasExcluded == isExcluded)
                    continue;

                var newExclusions = new List<Guid>(_originalExclusions[user.Id]);
                if (isExcluded)
                    newExclusions.Add(LibraryId);
                else
                    newExclusions.Remove(LibraryId);

                await K7ServerService.UpdateUserLibraryExclusionsAsync(user.Id, new UpdateUserLibraryExclusionsRequest
                {
                    ExcludedLibraryIds = newExclusions
                });
            }

            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Erreur : {ex.Message}", K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }
}
