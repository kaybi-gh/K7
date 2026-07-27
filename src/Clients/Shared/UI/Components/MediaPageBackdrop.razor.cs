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

    /// <summary>
    /// Optional full-resolution backdrop. Used when viewport CSS width * DPR exceeds the hero budget.
    /// </summary>
    [Parameter]
    public string? HighResImageUrl { get; set; }

    [Parameter]
    public string? SecondaryImageUrl { get; set; }

    [Parameter]
    public string? SecondaryHighResImageUrl { get; set; }

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

    [Parameter]
    public bool SoftStillBlurEnabled { get; set; }

    private ElementReference _rootRef;
    private IJSObjectReference? _module;
    private bool _scrollAttached;
    private bool _softStillAttached;
    private bool _heroPickAttached;
    private string? _attachedSoftStillImageUrl;
    private string? _resolvedImageUrl;
    private string? _resolvedSecondaryImageUrl;
    private int _primarySwapGeneration;
    private int _secondarySwapGeneration;
    private DotNetObjectReference<MediaPageBackdrop>? _dotNetRef;

    private string StyleAttribute => DominantColorCss.ToVariableStyle("--media-dominant-color", DominantColor);

    protected override async Task OnParametersSetAsync()
    {
        if (_module is null)
            return;

        await RefreshResolvedUrlsAsync();
    }

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

        if (!_heroPickAttached)
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync(
                "attachHeroImagePicker",
                _rootRef,
                _dotNetRef,
                MetadataPictureDisplayHelper.HeroBackdropPixelBudget);
            _heroPickAttached = true;
        }

        await RefreshResolvedUrlsAsync();

        var softStillUrl = _resolvedImageUrl ?? ImageUrl;
        if (SoftStillBlurEnabled
            && !string.IsNullOrEmpty(softStillUrl)
            && (!_softStillAttached || _attachedSoftStillImageUrl != softStillUrl))
        {
            await _module.InvokeAsync<bool>(
                "attachSoftStillBlur",
                _rootRef,
                softStillUrl,
                SoftStillSourceWidth,
                SoftStillSourceHeight,
                SoftStillMaxBlurPx);

            _softStillAttached = true;
            _attachedSoftStillImageUrl = softStillUrl;
        }
    }

    [JSInvokable]
    public Task OnHeroViewportChangedAsync() => RefreshResolvedUrlsAsync();

    private async Task RefreshResolvedUrlsAsync()
    {
        if (_module is null)
            return;

        var primary = await _module.InvokeAsync<string?>(
            "pickHeroImageUrl",
            ImageUrl,
            HighResImageUrl,
            MetadataPictureDisplayHelper.HeroBackdropPixelBudget);

        var secondary = await _module.InvokeAsync<string?>(
            "pickHeroImageUrl",
            SecondaryImageUrl,
            SecondaryHighResImageUrl,
            MetadataPictureDisplayHelper.HeroBackdropPixelBudget);

        var primaryChanged = !string.Equals(primary, _resolvedImageUrl, StringComparison.Ordinal);
        var secondaryChanged = !string.Equals(secondary, _resolvedSecondaryImageUrl, StringComparison.Ordinal);
        if (!primaryChanged && !secondaryChanged)
            return;

        if (primaryChanged)
            await ApplyPrimaryUrlAsync(primary);

        if (secondaryChanged)
            await ApplySecondaryUrlAsync(secondary);
    }

    private async Task ApplyPrimaryUrlAsync(string? nextUrl)
    {
        var generation = ++_primarySwapGeneration;

        if (string.IsNullOrEmpty(nextUrl))
        {
            _resolvedImageUrl = null;
            await InvokeAsync(StateHasChanged);
            return;
        }

        // Keep the current image visible until the next URL is decoded.
        if (_resolvedImageUrl is not null && _module is not null)
            await _module.InvokeAsync<bool>("preloadImage", nextUrl);

        if (generation != _primarySwapGeneration)
            return;

        _resolvedImageUrl = nextUrl;
        await InvokeAsync(StateHasChanged);
    }

    private async Task ApplySecondaryUrlAsync(string? nextUrl)
    {
        var generation = ++_secondarySwapGeneration;

        if (string.IsNullOrEmpty(nextUrl))
        {
            _resolvedSecondaryImageUrl = null;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_resolvedSecondaryImageUrl is not null && _module is not null)
            await _module.InvokeAsync<bool>("preloadImage", nextUrl);

        if (generation != _secondarySwapGeneration)
            return;

        _resolvedSecondaryImageUrl = nextUrl;
        await InvokeAsync(StateHasChanged);
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

        _dotNetRef?.Dispose();
    }
}
