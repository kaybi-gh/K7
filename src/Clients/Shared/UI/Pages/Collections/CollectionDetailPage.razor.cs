using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
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
    private List<MediaCardViewModel> _items = [];
    private string? _coverUrl;
    private bool _loading = true;
    private bool _loadingItems = true;
    private bool _canTrackProgress;
    private bool _canExclude;
    private bool _canSetWatchState;
    private bool _isAdmin;

    protected override async Task OnParametersSetAsync()
    {
        _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);
        (_canExclude, _isAdmin) = await MediaCardExcludeActions.LoadPermissionsAsync(FeatureAccess);
        _canSetWatchState = await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);

        _loading = true;
        _items.Clear();

        if (!Guid.TryParse(Id, out var id))
        {
            _loading = false;
            return;
        }

        _collection = await K7ServerService.GetCollectionAsync(id);

        if (_collection is not null)
        {
            _coverUrl = ApiClient.GetAbsoluteUri(
                _collection.CoverPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
            await LoadItemsAsync(id);
        }

        _loading = false;
    }

    private async Task LoadItemsAsync(Guid collectionId)
    {
        _loadingItems = true;
        var page = await K7ServerService.GetCollectionItemsAsync(collectionId, pageSize: 500);
        if (page?.Items is not null)
        {
            foreach (var item in page.Items)
            {
                if (item.Media.ToCardViewModel(ApiClient, n => string.Format(S["SeasonNumber"], n)) is { } vm)
                    _items.Add(vm);
            }
        }
        _loadingItems = false;
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
        MediaCardKind.Cover => item.ParentId is not null
            ? $"/music/albums/{item.Id}"
            : $"/music/albums/{item.Id}",
        _ => $"/movies/{item.Id}"
    };

    private static MediaCardVariant GetVariant(MediaCardViewModel item) =>
        item.Kind == MediaCardKind.Cover ? MediaCardVariant.Cover : MediaCardVariant.Poster;

    private async Task ExcludeForSelf(MediaCardViewModel item)
    {
        if (await MediaCardExcludeActions.ExcludeForSelfAsync(item, UserAdminService, Snackbar, S))
            _items.RemoveAll(m => m.Id == item.Id || m.ParentId == item.Id);
    }

    private Task ExcludeForOthers(MediaCardViewModel item) =>
        MediaCardExcludeActions.ExcludeForOthersAsync(item, DialogService, Snackbar, S);
}
