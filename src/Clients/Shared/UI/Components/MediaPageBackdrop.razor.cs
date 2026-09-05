using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.UI.Helpers;
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
    /// Optional full-resolution backdrop. Used when viewport CSS width exceeds the hero budget.
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
    private readonly string?[] _layerUrls = [null, null];
    private readonly bool[] _layerSoft = [false, false];
    private int _activeLayer;
    private int _swapGeneration;
    private bool _focusedLayerVisible;
    private bool _hasRequestedFocusedSwap;
    private string? _pendingFocusedUrl;
    private bool _pendingFocusedSoft;
    private bool _hasPendingFocusedImage;
    private DebouncedActionRunner? _focusedLayerSettleRunner;
    private DotNetObjectReference<MediaPageBackdrop>? _dotNetRef;
    private volatile bool _disposed;

    private string StyleAttribute => DominantColorCss.ToVariableStyle("--media-dominant-color", DominantColor);

    protected override void OnInitialized()
    {
        _focusedLayerSettleRunner = new DebouncedActionRunner(
            SwapPendingFocusedLayerAsync,
            InvokeAsync,
            TvHeroFocusSettle.DelayMs);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_disposed || _module is null)
            return;

        await RefreshResolvedUrlsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
            return;

        try
        {
            _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/mediaPageBackdrop.js");

            if (_disposed || _module is null)
                return;

            if (ScrollFadeEnabled && !_scrollAttached)
            {
                var attached = await _module.InvokeAsync<bool>("attachScrollFade", ScrollTarget, _rootRef);
                if (_disposed)
                    return;
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
                if (_disposed)
                    return;
                _heroPickAttached = true;
            }

            await RefreshResolvedUrlsAsync();
            if (_disposed || _module is null)
                return;

            await _module.InvokeVoidAsync("bindDecodedImages", _rootRef);

            if (_hasPendingFocusedImage)
            {
                var pendingUrl = _pendingFocusedUrl;
                var pendingSoft = _pendingFocusedSoft;
                _hasPendingFocusedImage = false;
                _hasRequestedFocusedSwap = true;
                await SwapFocusedLayerAsync(pendingUrl, pendingSoft);
            }

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

                if (_disposed)
                    return;

                _softStillAttached = true;
                _attachedSoftStillImageUrl = softStillUrl;
            }
        }
        catch (Exception ex) when (IsBenignJsInteropFailure(ex))
        {
            // Navigate away / WebView hide during native video: module may already be disposed.
        }
    }

    [JSInvokable]
    public Task OnHeroViewportChangedAsync() =>
        _disposed ? Task.CompletedTask : RefreshResolvedUrlsAsync();

    /// <summary>
    /// Crossfade a TV still without a parent re-render. Incoming JPEG is decoded
    /// off-screen first so D-pad moves do not paint progressive bands.
    /// Rapid focus changes debounce the decode so skipped cards are not fetched.
    /// </summary>
    public void ApplyFocusedImage(string? url, bool soft = false)
    {
        if (_disposed)
            return;

        _pendingFocusedUrl = url;
        _pendingFocusedSoft = soft;

        if (_module is null)
        {
            _hasPendingFocusedImage = true;
            return;
        }

        RequestFocusedLayerSwap();
    }

    private void RequestFocusedLayerSwap()
    {
        var url = _pendingFocusedUrl;
        var soft = _pendingFocusedSoft;

        if (string.IsNullOrEmpty(url))
        {
            _swapGeneration++;
            _ = SwapFocusedLayerAsync(url, soft);
            return;
        }

        if (!_hasRequestedFocusedSwap)
        {
            _hasRequestedFocusedSwap = true;
            _ = SwapFocusedLayerAsync(url, soft);
            return;
        }

        _swapGeneration++;
        _focusedLayerSettleRunner?.Schedule();
    }

    private Task SwapPendingFocusedLayerAsync() =>
        SwapFocusedLayerAsync(_pendingFocusedUrl, _pendingFocusedSoft);

    private async Task RefreshResolvedUrlsAsync()
    {
        if (_disposed || _module is null)
            return;

        try
        {
            var primary = await _module.InvokeAsync<string?>(
                "pickHeroImageUrl",
                ImageUrl,
                HighResImageUrl,
                MetadataPictureDisplayHelper.HeroBackdropPixelBudget);

            if (_disposed || _module is null)
                return;

            var secondary = await _module.InvokeAsync<string?>(
                "pickHeroImageUrl",
                SecondaryImageUrl,
                SecondaryHighResImageUrl,
                MetadataPictureDisplayHelper.HeroBackdropPixelBudget);

            if (_disposed)
                return;

            var primaryChanged = !string.Equals(primary, _resolvedImageUrl, StringComparison.Ordinal);
            var secondaryChanged = !string.Equals(secondary, _resolvedSecondaryImageUrl, StringComparison.Ordinal);
            if (!primaryChanged && !secondaryChanged)
                return;

            // Decode off-screen (opacity 0) then fade in via k7-img-decoded.
            // Do not wait here: the wash color is already visible and TV input
            // must not stall on a 1920px JPEG.
            if (primaryChanged)
            {
                _resolvedImageUrl = primary;
                await SafeStateHasChangedAsync();
            }

            if (secondaryChanged)
            {
                _resolvedSecondaryImageUrl = secondary;
                await SafeStateHasChangedAsync();
            }
        }
        catch (Exception ex) when (IsBenignJsInteropFailure(ex))
        {
        }
    }

    private async Task SwapFocusedLayerAsync(string? url, bool soft)
    {
        if (_disposed)
            return;

        if (string.IsNullOrEmpty(url))
        {
            if (!_focusedLayerVisible && _layerUrls[0] is null && _layerUrls[1] is null)
                return;

            _swapGeneration++;
            _layerUrls[0] = null;
            _layerUrls[1] = null;
            _focusedLayerVisible = false;
            await SafeStateHasChangedAsync();
            return;
        }

        if (_focusedLayerVisible
            && url == _layerUrls[_activeLayer]
            && soft == _layerSoft[_activeLayer])
            return;

        var targetLayer = 1 - _activeLayer;
        var generation = ++_swapGeneration;

        _layerUrls[targetLayer] = url;
        _layerSoft[targetLayer] = soft;
        await SafeStateHasChangedAsync();

        try
        {
            await JSRuntime.InvokeVoidAsync("K7.preloadImage", url);
        }
        catch (Exception ex) when (IsBenignJsInteropFailure(ex))
        {
            return;
        }

        if (_disposed || generation != _swapGeneration)
            return;

        _activeLayer = targetLayer;
        _focusedLayerVisible = true;
        await SafeStateHasChangedAsync();
    }

    private async Task SafeStateHasChangedAsync()
    {
        if (_disposed)
            return;

        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex) when (IsBenignJsInteropFailure(ex) || ex is ObjectDisposedException)
        {
        }
    }

    private static bool IsBenignJsInteropFailure(Exception ex) =>
        ex is JSDisconnectedException
            or ObjectDisposedException
            or JSException
            or InvalidOperationException;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _swapGeneration++;
        _hasPendingFocusedImage = false;
        _focusedLayerSettleRunner?.Dispose();
        _focusedLayerSettleRunner = null;

        var module = _module;
        _module = null;
        _scrollAttached = false;
        _softStillAttached = false;
        _heroPickAttached = false;

        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", _rootRef);
                await module.DisposeAsync();
            }
            catch (Exception ex) when (IsBenignJsInteropFailure(ex))
            {
            }
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }
}
