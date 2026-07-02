using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;

using K7.Clients.Shared.Mappings;

using K7.Clients.Shared.Models;

using K7.Clients.Shared.UI.Components;

using K7.Clients.Shared.UI.Components.Dialogs;

using K7.Server.Domain.Enums;

using K7.Shared.Dtos.Entities.Collections;

using K7.Shared.Dtos.Entities.Medias;

using Microsoft.AspNetCore.Components;



namespace K7.Clients.Shared.UI.Pages.Collections;



public partial class CollectionDetailPage

{

    [Parameter] public required string Id { get; set; }



    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;

    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;



    private CollectionDto? _collection;

    private List<CollectionBrowseRow> _browseRows = [];

    private IReadOnlyList<string> _headerPreviewUrls = [];

    private bool _loading = true;

    private bool _loadingItems = true;

    private bool _canTrackProgress;

    private bool _canExclude;

    private bool _canSetWatchState;

    private bool _isAdmin;

    private BrowseView<CollectionBrowseRow>? _browseView;

    private K7DataTable<CollectionBrowseRow>? _dataTable;

    private string? _activeSortKey = "order";

    private K7SortDirection _activeSortDirection = K7SortDirection.Ascending;



    private bool _showHeaderPlaceholder => _browseRows.Count == 0;



    internal sealed record CollectionBrowseRow(Guid ItemId, LiteMediaDto Media, int Index);



    protected override async Task OnParametersSetAsync()

    {

        _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);

        (_canExclude, _isAdmin) = await MediaCardExcludeActions.LoadPermissionsAsync(FeatureAccess);

        _canSetWatchState = await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);



        _loading = true;

        _browseRows.Clear();

        _headerPreviewUrls = [];



        if (!Guid.TryParse(Id, out var id))

        {

            _loading = false;

            return;

        }



        _collection = await K7ServerService.GetCollectionAsync(id);



        if (_collection is not null)

            await LoadItemsAsync(id);



        _loading = false;

    }



    private async Task LoadItemsAsync(Guid collectionId)

    {

        _loadingItems = true;

        var page = await K7ServerService.GetCollectionItemsAsync(collectionId, pageSize: 500);

        _browseRows.Clear();



        if (page?.Items is not null)

        {

            var index = 0;

            foreach (var item in page.Items)

            {

                _browseRows.Add(new CollectionBrowseRow(item.Id, item.Media, index));

                index++;

            }

        }



        _headerPreviewUrls = _browseRows

            .SelectMany(r => GetItemPreviewUrls(r.Media))

            .Take(4)

            .ToList();

        _loadingItems = false;

        if (_dataTable is not null)
            await _dataTable.RefreshAsync();
    }

    private Task<K7DataTableResult<CollectionBrowseRow>> LoadTableDataAsync(
        K7DataTableState<CollectionBrowseRow> state, CancellationToken cancellationToken)
    {
        if (state.Count <= 0)
            return Task.FromResult(new K7DataTableResult<CollectionBrowseRow>([], 0));

        var sorted = SortCollectionRows(_browseRows, state.SortKey, state.SortDirection);
        var items = sorted
            .Skip(state.StartIndex)
            .Take(state.Count)
            .ToList();

        return Task.FromResult(new K7DataTableResult<CollectionBrowseRow>(items, sorted.Count));
    }

    private static List<CollectionBrowseRow> SortCollectionRows(
        IReadOnlyList<CollectionBrowseRow> rows,
        string? sortKey,
        K7SortDirection direction)
    {
        var desc = direction is K7SortDirection.Descending;
        IEnumerable<CollectionBrowseRow> query = rows;

        query = sortKey switch
        {
            "title" => desc ? query.OrderByDescending(r => r.Media.Title) : query.OrderBy(r => r.Media.Title),
            "releaseDate" => desc
                ? query.OrderByDescending(r => r.Media.ReleaseDate)
                : query.OrderBy(r => r.Media.ReleaseDate),
            _ => desc ? query.OrderByDescending(r => r.Index) : query.OrderBy(r => r.Index)
        };

        return query.ToList();
    }

    private async Task OnTableSortChanged(SortChangedEventArgs args)
    {
        _activeSortKey = args.SortKey;
        _activeSortDirection = args.Direction;

        if (_dataTable is not null)
            await _dataTable.RefreshAsync();
    }

    private void NavigateToBrowseRow(CollectionBrowseRow row) =>
        NavigateToItem(row.Media);

    private void OnColumnPickerRequested() =>
        _dataTable?.ToggleColumnPicker();



    private async Task RemoveItemAsync(CollectionBrowseRow row)

    {

        if (_collection is null) return;



        try

        {

            await K7ServerService.RemoveCollectionItemAsync(_collection.Id, row.ItemId);

            _browseRows.Remove(row);

            ReindexBrowseRows();

            _collection = _collection with { ItemCount = _browseRows.Count };

            _headerPreviewUrls = _browseRows.SelectMany(r => GetItemPreviewUrls(r.Media)).Take(4).ToList();

            if (_dataTable is not null)
                await _dataTable.RefreshAsync();

        }

        catch

        {

            Snackbar.Add(L["DeleteError"], K7Severity.Error);

        }

    }



    private void ReindexBrowseRows()

    {

        _browseRows = _browseRows

            .Select((row, index) => row with { Index = index })

            .ToList();

    }



    private async Task OpenEditDialog()

    {

        if (_collection is null) return;



        var parameters = new K7DialogParameters<EditCollectionDialog>

        {

            { x => x.CollectionId, _collection.Id },

            { x => x.Title, _collection.Title },

            { x => x.Description, _collection.Description },

            { x => x.IsPublic, _collection.IsPublic },

            { x => x.CoverPictureId, _collection.CoverPicture?.Id }

        };



        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };

        var dialog = await DialogService.ShowAsync<EditCollectionDialog>(L["EditDialogTitle"], parameters, options);

        var result = await dialog.Result;



        if (result is { Canceled: false })

        {

            var collectionId = _collection.Id;

            _collection = await K7ServerService.GetCollectionAsync(collectionId);

            _browseRows.Clear();

            if (_collection is not null)

                await LoadItemsAsync(collectionId);

        }

    }



    private async Task ConfirmDelete()

    {

        if (_collection is null) return;



        var result = await DialogService.ShowMessageBoxAsync(

            L["DeleteConfirmTitle"],

            L["DeleteConfirmMessage"],

            yesText: S["Delete"], cancelText: S["Cancel"]);



        if (result != true) return;



        try

        {

            await K7ServerService.DeleteCollectionAsync(_collection.Id);

            Snackbar.Add(L["Deleted"], K7Severity.Success);

            NavigationManager.NavigateTo("/my-space/collections");

        }

        catch

        {

            Snackbar.Add(L["DeleteError"], K7Severity.Error);

        }

    }



    private static string GetItemHref(MediaCardViewModel item) => item.Kind switch

    {

        MediaCardKind.Serie => $"/series/{item.Id}",

        MediaCardKind.Cover => $"/music/albums/{item.Id}",

        _ => $"/movies/{item.Id}"

    };



    private static string GetLiteItemHref(LiteMediaDto item) => item switch

    {

        LiteMusicAlbumDto album => $"/music/albums/{album.Id}",

        LiteSerieDto serie => $"/series/{serie.Id}",

        _ => $"/movies/{item.Id}"

    };



    private void NavigateToItem(LiteMediaDto item) =>

        NavigationManager.NavigateTo(GetLiteItemHref(item));



    private static MediaCardVariant GetVariant(MediaCardViewModel item) =>

        item.Kind == MediaCardKind.Cover ? MediaCardVariant.Cover : MediaCardVariant.Poster;



    private IReadOnlyList<string> GetItemPreviewUrls(LiteMediaDto item)

    {

        var picture = item is LiteSerieEpisodeDto

            ? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)

                ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)

                ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)

                ?? item.Pictures?.FirstOrDefault()

            : item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)

                ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)

                ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)

                ?? item.Pictures?.FirstOrDefault();



        var url = ApiClient.GetAbsoluteUri(picture?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;

        return url is null ? [] : [url];

    }



    private async Task ExcludeForSelf(MediaCardViewModel item)

    {

        if (await MediaCardExcludeActions.ExcludeForSelfAsync(item, UserAdminService, Snackbar, S))

        {

            _browseRows.RemoveAll(r => r.Media.Id.ToString() == item.Id || r.Media.Id.ToString() == item.ParentId);

            ReindexBrowseRows();

            _headerPreviewUrls = _browseRows.SelectMany(r => GetItemPreviewUrls(r.Media)).Take(4).ToList();

            if (_dataTable is not null)
                await _dataTable.RefreshAsync();

        }

    }



    private Task ExcludeForOthers(MediaCardViewModel item) =>

        MediaCardExcludeActions.ExcludeForOthersAsync(item, DialogService, Snackbar, S);

}

