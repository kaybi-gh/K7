using K7.Clients.Shared.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class MediaPageBackdrop : IAsyncDisposable
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter]
    public string? ImageUrl { get; set; }

    [Parameter]
    public string? SecondaryImageUrl { get; set; }

    [Parameter]
    public string? SecondaryImageKey { get; set; }

    [Parameter]
    public string? DominantColor { get; set; }

    [Parameter]
    public ElementReference ScrollTarget { get; set; }

    [Parameter]
    public bool ScrollFadeEnabled { get; set; } = true;

    [Parameter]
    public string Class { get; set; } = "";

    private ElementReference _rootRef;
    private IJSObjectReference? _module;
    private bool _scrollAttached;

    private string StyleAttribute => DominantColorCss.ToVariableStyle("--media-dominant-color", DominantColor);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!ScrollFadeEnabled || _scrollAttached)
            return;

        _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/K7.Clients.Shared.UI/js/mediaPageBackdrop.js");

        await _module.InvokeVoidAsync("attachScrollFade", ScrollTarget, _rootRef);
        _scrollAttached = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose", _rootRef);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
