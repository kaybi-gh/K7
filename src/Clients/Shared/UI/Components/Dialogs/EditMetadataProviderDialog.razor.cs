using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class EditMetadataProviderDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter] public string LibraryTitle { get; set; } = "";
    [Parameter] public List<MetadataProviderInfoDto> AvailableProviders { get; set; } = [];
    [Parameter] public string? SelectedProvider { get; set; }

    private void Submit() => MudDialog.Close(DialogResult.Ok(SelectedProvider));
    private void Cancel() => MudDialog.Cancel();
}
