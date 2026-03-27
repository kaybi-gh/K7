using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components;

public partial class MediaPoster
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    [Parameter] public bool Skeleton { get; set; } = false;
    [Parameter] public required MediaPosterViewModel Model { get; set; }
    [Parameter] public bool OverlayEnabled { get; set; } = false;
    [Parameter] public bool ProgressEnabled { get; set; } = false;
    [Parameter] public bool WatchedStatusEnabled { get; set; } = false;
    [Parameter] public bool FooterVisible { get; set; } = false;
    [Parameter] public EventCallback OnExcluded { get; set; }

    private bool _canExclude;
    private bool _isAdmin;

    protected override async Task OnInitializedAsync()
    {
        var role = await FeatureAccess.GetRoleAsync();
        _canExclude = role is not null and not K7.Server.Domain.Constants.Roles.Guest;
        _isAdmin = role == K7.Server.Domain.Constants.Roles.Administrator;
    }

    private bool ProgressBarIsHidden() => Model.Progress == 0 || Model.Progress == 100;

    private async Task OnExcludeForSelf()
    {
        try
        {
            var excluded = await K7ServerService.ToggleMediaExclusionAsync(Guid.Parse(Model.Id));
            if (excluded)
            {
                Snackbar.Add($"« {Model.Title} » masqué", Severity.Success);
                await OnExcluded.InvokeAsync();
            }
            else
            {
                Snackbar.Add($"« {Model.Title} » affiché à nouveau", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Erreur : {ex.Message}", Severity.Error);
        }
    }

    private async Task OnExcludeForOthers()
    {
        var parameters = new DialogParameters<Dialogs.ExcludeMediaForUsersDialog>
        {
            { x => x.MediaId, Guid.Parse(Model.Id) },
            { x => x.MediaTitle, Model.Title }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<Dialogs.ExcludeMediaForUsersDialog>("Masquer pour un utilisateur", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add("Exclusions mises à jour.", Severity.Success);
        }
    }
}
