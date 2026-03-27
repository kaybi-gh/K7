using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class AdminUserLibraryExclusionsDialog
{
    [Inject] private ILibraryService K7ServerService { get; set; } = default!;

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public List<Guid> ExcludedLibraryIds { get; set; } = [];

    private bool _loading = true;
    private List<LibraryDto> _libraries = [];
    private HashSet<Guid> _excludedIds = [];

    protected override async Task OnInitializedAsync()
    {
        _excludedIds = new HashSet<Guid>(ExcludedLibraryIds);
        try
        {
            _libraries = await K7ServerService.GetLibrariesAsync();
        }
        catch
        {
            _libraries = [];
        }
        _loading = false;
    }

    private void ToggleExclusion(Guid libraryId, bool exclude)
    {
        if (exclude)
            _excludedIds.Add(libraryId);
        else
            _excludedIds.Remove(libraryId);
    }

    private void Cancel() => MudDialog.Cancel();
    private void Submit() => MudDialog.Close(DialogResult.Ok(_excludedIds.ToList()));
}
