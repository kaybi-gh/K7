using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class SpatialNavSection : IAsyncDisposable
{
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    /// <summary>Unique section identifier. Must be stable across re-renders.</summary>
    [Parameter, EditorRequired] public string SectionId { get; set; } = default!;

    /// <summary>How navigation enters this section: last-focused (default) | first | default-element.</summary>
    [Parameter] public string EnterTo { get; set; } = "last-focused";

    /// <summary>Focus restriction: self-first (default) | self-only | none.</summary>
    [Parameter] public string Restrict { get; set; } = "self-first";

    /// <summary>Optional CSS selector override. Defaults to [data-sn-section="{SectionId}"] focusables.</summary>
    [Parameter] public string? Selector { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            await SpatialNav.AddSectionAsync(SectionId, new SpatialNavSectionOptions
            {
                Selector = Selector,
                EnterTo = EnterTo,
                Restrict = Restrict
            });
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await SpatialNav.RemoveSectionAsync(SectionId);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
    }
}