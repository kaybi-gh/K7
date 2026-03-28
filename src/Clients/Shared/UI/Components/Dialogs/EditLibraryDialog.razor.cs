using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class EditLibraryDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter] public string Title { get; set; } = "";
    [Parameter] public List<MetadataProviderInfoDto> AvailableProviders { get; set; } = [];
    [Parameter] public string? SelectedProvider { get; set; }

    private bool CanSubmit => !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(SelectedProvider);

    private void Submit()
    {
        var result = new UpdateLibraryRequest
        {
            Title = Title.Trim(),
            MetadataProviderName = SelectedProvider
        };
        MudDialog.Close(DialogResult.Ok(result));
    }

    private void Cancel() => MudDialog.Cancel();
}
