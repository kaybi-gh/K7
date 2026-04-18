using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class EditLibraryDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public string Title { get; set; } = "";
    [Parameter] public List<MetadataProviderInfoDto> AvailableProviders { get; set; } = [];
    [Parameter] public string? SelectedProvider { get; set; }
    [Parameter] public int? MetadataRefreshIntervalDays { get; set; }

    private bool CanSubmit => !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(SelectedProvider);

    private void Submit()
    {
        var result = new UpdateLibraryRequest
        {
            Title = Title.Trim(),
            MetadataProviderName = SelectedProvider,
            MetadataRefreshIntervalDays = MetadataRefreshIntervalDays
        };
        Dialog.Close(K7DialogResult.Ok(result));
    }

    private void Cancel() => Dialog.Cancel();
}
