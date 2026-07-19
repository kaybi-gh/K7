using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpacePage
{
    [Inject] private IPlaylistService PlaylistService { get; set; } = default!;
    [Inject] private ICollectionService CollectionService { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;

    private int _playlistCount;
    private int _collectionCount;
    private bool _canRate;

    private string PlaylistDescription =>
        _playlistCount > 0 ? string.Format(L["PlaylistsCount"], _playlistCount) : L["PlaylistsDesc"];

    private string HistoryDescription => L["HistoryDesc"];

    private string CollectionsDescription =>
        _collectionCount > 0 ? string.Format(L["CollectionsCount"], _collectionCount) : L["CollectionsDesc"];

    protected override async Task OnInitializedAsync()
    {
        _canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);
        await Task.WhenAll(
            LoadPlaylistsCountAsync(),
            LoadCollectionsCountAsync());
    }

    private async Task LoadPlaylistsCountAsync()
    {
        try
        {
            var result = await PlaylistService.GetPlaylistsAsync(pageSize: 1);
            _playlistCount = result?.TotalCount ?? result?.Items?.Count ?? 0;
        }
        catch
        {
            _playlistCount = 0;
        }
    }

    private async Task LoadCollectionsCountAsync()
    {
        try
        {
            var result = await CollectionService.GetCollectionsAsync(pageSize: 1);
            _collectionCount = result?.TotalCount ?? result?.Items?.Count ?? 0;
        }
        catch
        {
            _collectionCount = 0;
        }
    }

}
