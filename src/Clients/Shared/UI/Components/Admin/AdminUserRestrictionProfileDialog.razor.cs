using K7.Shared.Dtos.Restrictions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class AdminUserRestrictionProfileDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter] public Guid? CurrentProfileId { get; set; }

    private bool _isLoading = true;
    private List<ContentRestrictionProfileDto> _profiles = [];
    private Guid? _selectedProfileId;

    protected override async Task OnInitializedAsync()
    {
        _selectedProfileId = CurrentProfileId;
        try
        {
            _profiles = await K7ServerService.GetContentRestrictionProfilesAsync();
        }
        catch
        {
            _profiles = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void Cancel() => MudDialog.Cancel();

    private void Submit() => MudDialog.Close(DialogResult.Ok(_selectedProfileId));
}
