using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Collections;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class AddToCollectionDialog
{
    [Inject] private ICollectionService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public Guid MediaId { get; set; }

    [Parameter]
    public MediaType? MediaType { get; set; }

    private List<LiteCollectionDto> _collections = [];
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadCollectionsAsync();
    }

    private async Task LoadCollectionsAsync()
    {
        _loading = true;
        var result = await K7ServerService.GetCollectionsAsync(1, 100, MediaType);
        _collections = result?.Items?.ToList() ?? [];
        _loading = false;
    }

    private async Task SelectCollection(LiteCollectionDto collection)
    {
        try
        {
            await K7ServerService.AddCollectionItemAsync(collection.Id, MediaId);
            Snackbar.Add(string.Format(L["AddedToCollection"], collection.Title), K7Severity.Success);
            Dialog.Close(K7DialogResult.Ok(collection.Id));
        }
        catch
        {
            Snackbar.Add(L["AddError"], K7Severity.Error);
        }
    }

    private async Task CreateNewCollection()
    {
        var parameters = new K7DialogParameters<CreateCollectionDialog>
        {
            { x => x.DefaultMediaType, MediaType }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreateCollectionDialog>(L["NewCollectionTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: Guid newId })
        {
            try
            {
                await K7ServerService.AddCollectionItemAsync(newId, MediaId);
                Snackbar.Add(L["AddedToNewCollection"], K7Severity.Success);
                Dialog.Close(K7DialogResult.Ok(newId));
            }
            catch
            {
                Snackbar.Add(L["CreatedButAddFailed"], K7Severity.Warning);
                Dialog.Close(K7DialogResult.Ok(newId));
            }
        }
        else
        {
            await LoadCollectionsAsync();
        }
    }

    private void Cancel() => Dialog.Cancel();
}
