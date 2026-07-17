using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class AcceptPeerRequestDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    private bool _isLoading = true;
    private bool _autoShareNew;
    private List<LibraryDto> _libraries = [];
    private HashSet<Guid> _selectedIds = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _libraries = await LibraryService.GetLibrariesAsync();
            _selectedIds = _libraries.Select(l => l.Id).ToHashSet();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ToggleLibrary(Guid id)
    {
        if (!_selectedIds.Remove(id))
            _selectedIds.Add(id);
    }

    private void Cancel() => Dialog.Cancel();

    private void Submit() => Dialog.Close(K7DialogResult.Ok(new AcceptPeerResult(_selectedIds.ToList(), _autoShareNew)));
}
