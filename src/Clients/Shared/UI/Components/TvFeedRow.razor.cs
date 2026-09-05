using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class TvFeedRow
{
    [CascadingParameter] private TvVerticalWindow? Window { get; set; }

    [Parameter] public int Index { get; set; }
    [Parameter] public bool KeepMounted { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private readonly TvFeedRowViewport _viewport = new();

    private bool ShouldRenderContent => Window?.ShouldRender(Index) ?? true;

    private string _rootClass => Window is null
        ? "tv-feed-row tv-feed-row--passthrough"
        : "tv-feed-row";

    protected override void OnParametersSet() =>
        _viewport.RenderContent = ShouldRenderContent;
}
