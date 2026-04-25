using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpaceCollectionsPage
{
    private List<LiteCollectionDto> _collections = [];
    private bool _loading = true;

    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadCollectionsAsync();
    }

    private async Task LoadCollectionsAsync()
    {
        _loading = true;
        var result = await K7ServerService.GetCollectionsAsync();
        _collections = result?.Items?.ToList() ?? [];
        _loading = false;
    }

    private async Task OpenCreateCollectionDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreateCollectionDialog>("Nouvelle collection", null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            await LoadCollectionsAsync();
    }

    private void GoToCollection(LiteCollectionDto collection)
    {
        NavigationManager.NavigateTo($"/collections/{collection.Id}");
    }

    private void OnCollectionKeyDown(KeyboardEventArgs e, LiteCollectionDto collection)
    {
        if (e.Code is "Enter" or "Space")
            GoToCollection(collection);
    }

    private string? GetCoverUrl(LiteCollectionDto collection)
    {
        var uri = collection.CoverPicture?.GetUri(MetadataPictureSize.Small)?.OriginalString;
        return ApiClient.GetAbsoluteUri(uri)?.AbsoluteUri;
    }
}
