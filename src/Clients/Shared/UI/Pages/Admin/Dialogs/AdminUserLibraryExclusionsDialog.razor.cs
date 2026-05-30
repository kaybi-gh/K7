using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class AdminUserLibraryExclusionsDialog
{
    [Inject] private ILibraryService K7ServerService { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;
    [Parameter] public List<Guid> ExcludedLibraryIds { get; set; } = [];

    private bool _loading = true;
    private List<LibraryGroupDto> _groups = [];
    private List<LibraryDto> _libraries = [];
    private HashSet<Guid> _excludedIds = [];

    protected override async Task OnInitializedAsync()
    {
        _excludedIds = new HashSet<Guid>(ExcludedLibraryIds);
        try
        {
            var groupsTask = K7ServerService.GetLibraryGroupsAsync();
            var librariesTask = K7ServerService.GetLibrariesAsync();
            await Task.WhenAll(groupsTask, librariesTask);
            _groups = groupsTask.Result;
            _libraries = librariesTask.Result;
        }
        catch
        {
            _groups = [];
            _libraries = [];
        }
        _loading = false;
    }

    private IEnumerable<LibraryDto> GetLibrariesForGroup(LibraryGroupDto group) =>
        _libraries.Where(l => l.LibraryGroupId == group.Id);

    private void ToggleExclusion(Guid libraryId, bool exclude)
    {
        if (exclude)
            _excludedIds.Add(libraryId);
        else
            _excludedIds.Remove(libraryId);
    }

    private void Cancel() => Dialog.Cancel();
    private void Submit() => Dialog.Close(K7DialogResult.Ok(_excludedIds.ToList()));
}
