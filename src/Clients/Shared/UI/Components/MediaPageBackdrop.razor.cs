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

    [Parameter]
    public int? SoftStillSourceWidth { get; set; }

    [Parameter]
    public int? SoftStillSourceHeight { get; set; }

    [Parameter]
    public double SoftStillMaxBlurPx { get; set; } = 10;

    private ElementReference _rootRef;
    private IJSObjectReference? _module;
    private bool _scrollAttached;
    private bool _softStillAttached;
    private int? _attachedSoftStillWidth;
    private int? _attachedSoftStillHeight;

    private string StyleAttribute => DominantColorCss.ToVariableStyle("--media-dominant-color", DominantColor);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/K7.Clients.Shared.UI/js/mediaPageBackdrop.js");

        if (ScrollFadeEnabled && !_scrollAttached)
        {
            var attached = await _module.InvokeAsync<bool>("attachScrollFade", ScrollTarget, _rootRef);
            if (attached)
                _scrollAttached = true;
        }

        if (SoftStillSourceWidth is > 0
            && SoftStillSourceHeight is > 0
            && (!_softStillAttached
                || _attachedSoftStillWidth != SoftStillSourceWidth
                || _attachedSoftStillHeight != SoftStillSourceHeight))
        {
            await _module.InvokeAsync<bool>(
                "attachSoftStillBlur",
                _rootRef,
                SoftStillSourceWidth,
                SoftStillSourceHeight,
                SoftStillMaxBlurPx);

            _softStillAttached = true;
            _attachedSoftStillWidth = SoftStillSourceWidth;
            _attachedSoftStillHeight = SoftStillSourceHeight;
        }
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
