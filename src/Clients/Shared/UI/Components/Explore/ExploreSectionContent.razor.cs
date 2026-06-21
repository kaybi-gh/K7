using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Explore;

public partial class ExploreSectionContent
{
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    [Parameter] public RenderFragment ChildContent { get; set; } = default!;
    [Parameter] public bool IsTv { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string? GroupTitle { get; set; }
    [Parameter] public string? BrowseHref { get; set; }
    [Parameter] public string? BrowseLabel { get; set; }
    [Parameter] public string? BackAriaLabel { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }

    private ExploreTvHeroPanel? _heroPanel;
    private ExploreTvFocusContext? _tvFocusContext;
    private bool _focusApplied;
    private string _carouselKey = "loading";

    protected override void OnParametersSet()
    {
        var nextKey = IsLoading ? "loading" : "content";
        if (_carouselKey != nextKey)
        {
            _carouselKey = nextKey;
            _focusApplied = false;
        }
    }

    protected override void OnInitialized()
    {
        _tvFocusContext = new ExploreTvFocusContext
        {
            OnItemFocused = OnTvItemFocused,
            TrySetInitialItem = TrySetInitialItem,
            UseDetailedFeed = true
        };
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!IsTv || IsLoading || _focusApplied)
            return;

        try
        {
            await SpatialNav.FocusFirstAsync("[data-carousel-item] a, [data-carousel-item] button");
            _focusApplied = true;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnTvItemFocused(MediaCardViewModel item) => _heroPanel?.NotifyFocused(item);

    private void TrySetInitialItem(MediaCardViewModel item) => _heroPanel?.TrySetInitialItem(item);
}
