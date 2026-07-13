using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Explore;

public partial class ExploreLibraryGroupView : IDisposable
{
    [Inject] private IExploreGroupStore ExploreGroupStore { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter, EditorRequired] public LibraryGroupDto Group { get; set; } = default!;
    [Parameter] public bool IsTv { get; set; }

    private List<MediaTagValueDto> _genres = [];
    private Guid[] _libraryGroupIds = [];
    private Guid[] _libraryIds = [];
    private bool _loading = true;
    private Guid _loadedGroupId;

    private string BrowseHref => $"/library-groups/{Group.Id}";

    private static readonly HashSet<MediaType> _movieTypes = [MediaType.Movie];
    private static readonly HashSet<MediaType> _serieTypes = [MediaType.Serie];
    private static readonly HashSet<MediaType> _musicAlbumTypes = [MediaType.MusicAlbum];

    private static readonly HashSet<MediaOrderingOption> CreatedOrder = [MediaOrderingOption.CreatedDesc];
    private static readonly HashSet<MediaOrderingOption> TrendingOrder = [MediaOrderingOption.TrendingDesc];
    private static readonly HashSet<MediaOrderingOption> ProviderRatingOrder = [MediaOrderingOption.ProviderRatingDesc];

    protected override void OnInitialized() => ExploreGroupStore.Changed += OnExploreGroupChanged;

    protected override async Task OnParametersSetAsync()
    {
        if (Group.Id == _loadedGroupId)
            return;

        await LoadGroupAsync(showLoading: true);
    }

    public void Dispose() => ExploreGroupStore.Changed -= OnExploreGroupChanged;

    private void OnExploreGroupChanged(Guid groupId)
    {
        if (groupId != Group.Id)
            return;

        _ = InvokeAsync(() => LoadGroupAsync(showLoading: false));
    }

    private async Task LoadGroupAsync(bool showLoading)
    {
        if (showLoading)
            _loading = true;

        _loadedGroupId = Group.Id;
        _libraryGroupIds = [Group.Id];
        _libraryIds = Group.LibraryIds.ToArray();

        var snapshot = await ExploreGroupStore.EnsureGroupAsync(Group.Id);
        _genres = snapshot?.Genres.ToList() ?? [];

        _loading = false;
    }

    private void GoBack() => NavigationManager.NavigateTo("/explore");
}
