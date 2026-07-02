using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpaceCollectionsPage
{
    private const string FilterStorageKey = "my-space-collections";
    private const int PageSize = 500;

    private List<LiteCollectionDto> _collections = [];
    private bool _loading = true;
    private bool _canCreate;
    private LibraryItemOrderingOption _selectedSort = LibraryItemOrderingOption.LastModifiedDesc;

    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _canCreate = await FeatureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist);
        await LoadPersistedFiltersAsync();
        await LoadCollectionsAsync();
    }

    private async Task LoadCollectionsAsync()
    {
        _loading = true;
        var result = await K7ServerService.GetCollectionsAsync(pageSize: PageSize, orderBy: _selectedSort);
        _collections = result?.Items?.ToList() ?? [];
        _loading = false;
    }

    private async Task OnSortChanged(LibraryItemOrderingOption value)
    {
        if (value == _selectedSort)
            return;

        _selectedSort = value;
        await PersistFiltersAsync();
        await LoadCollectionsAsync();
    }

    private async Task LoadPersistedFiltersAsync()
    {
        try
        {
            var state = await PageFilterStorage.LoadAsync<MySpaceCollectionsFilterState>(FilterStorageKey);
            if (state is null)
                return;

            if (Enum.IsDefined(typeof(LibraryItemOrderingOption), state.Sort))
                _selectedSort = (LibraryItemOrderingOption)state.Sort;
        }
        catch
        {
            // Non-critical
        }
    }

    private async Task PersistFiltersAsync()
    {
        try
        {
            await PageFilterStorage.SaveAsync(
                FilterStorageKey,
                new MySpaceCollectionsFilterState((int)_selectedSort));
        }
        catch
        {
            // Non-critical
        }
    }

    private string GetSortLabel(LibraryItemOrderingOption option) =>
        MySpaceLibraryBrowseSort.GetLabel(option, LibrarySortL);

    private async Task OpenCreateCollectionDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreateCollectionDialog>("Nouvelle collection", null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            await LoadCollectionsAsync();
    }

    private string GetCollectionItemCountLabel(LiteCollectionDto collection) =>
        $"{collection.ItemCount} {L["Items"]}";

    private string GetCollectionHref(LiteCollectionDto collection) =>
        $"/collections/{collection.Id}";

    private string GetCollectionSubtitle(LiteCollectionDto collection)
    {
        var parts = new List<string> { $"{collection.ItemCount} {L["Items"]}" };
        if (collection.IsPublic)
            parts.Add(L["Public"]);
        return string.Join(" · ", parts);
    }

    private sealed record MySpaceCollectionsFilterState(int Sort);
}
