using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Image
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string Src { get; set; } = "";
    [Parameter] public string Alt { get; set; } = "";
    [Parameter] public string ObjectFit { get; set; } = "";
    [Parameter] public int Width { get; set; }
    [Parameter] public int Height { get; set; }
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public string Loading { get; set; } = "lazy";
    [Parameter] public bool Fluid { get; set; }
    [Parameter] public bool ShowLoadingState { get; set; } = true;
    [Parameter] public string FallbackIcon { get; set; } = Phosphor.ImageBroken;
    [Parameter] public RenderFragment? FallbackContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _imgRef;
    private string? _trackedSrc;
    private bool _loaded;
    private bool _failed;
    private bool _checkedCachedLoad;

    private bool HasSource => !string.IsNullOrEmpty(Src);
    private bool HasCustomFallback => FallbackContent is not null;
    private bool ShowFallback => !HasSource || _failed;
    private bool ShowLoading => HasSource && !_loaded && !_failed && ShouldShowLoadingState;
    private bool ShouldShowLoadingState => ShowLoadingState && !IsTinyThumbnail;
    private bool IsTinyThumbnail => (Width is > 0 and <= 40) || (Height is > 0 and <= 40);
    private bool UseLoadTransition => ShouldShowLoadingState && HasSource && !_failed;

    private string WrapCssClass =>
        string.Join(" ",
            new[]
            {
                "k7-img-wrap",
                Fluid ? "k7-img-wrap--fluid" : null,
                ShowFallback && HasCustomFallback ? "k7-img-wrap--custom-fallback" : null,
                _loaded ? "k7-img-wrap--loaded" : null,
                UseLoadTransition && !_loaded ? "k7-img-wrap--pending" : null,
                Class
            }.Where(static s => !string.IsNullOrEmpty(s)));

    private string WrapStyle
    {
        get
        {
            var parts = new List<string>();
            if (Width > 0)
                parts.Add($"width:{Width}px");
            if (Height > 0)
                parts.Add($"height:{Height}px");
            return string.Join("; ", parts);
        }
    }

    private string ImageCssClass =>
        string.Join(" ",
            new[]
            {
                "k7-img",
                ObjectFitClass,
                Fluid ? "k7-img--fluid" : null
            }.Where(static s => !string.IsNullOrEmpty(s)));

    private string ObjectFitClass => ObjectFit switch
    {
        "cover" => "k7-img--cover",
        "contain" => "k7-img--contain",
        "scale-down" => "k7-img--scale-down",
        _ => ""
    };

    private Dictionary<string, object?> ImageAttributes
    {
        get
        {
            var attributes = new Dictionary<string, object?>();
            if (Width > 0)
                attributes["width"] = Width;
            if (Height > 0)
                attributes["height"] = Height;
            if (!string.IsNullOrEmpty(Style))
                attributes["style"] = Style;
            if (AdditionalAttributes is not null)
            {
                foreach (var pair in AdditionalAttributes)
                    attributes[pair.Key] = pair.Value;
            }

            return attributes;
        }
    }

    protected override void OnParametersSet()
    {
        if (_trackedSrc == Src)
            return;

        _trackedSrc = Src;
        _loaded = false;
        _failed = false;
        _checkedCachedLoad = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_checkedCachedLoad || !HasSource || _loaded || _failed)
            return;

        _checkedCachedLoad = true;

        try
        {
            var isLoaded = await JS.InvokeAsync<bool>("K7.isImageLoaded", _imgRef);
            if (!isLoaded)
                return;

            _loaded = true;
            await InvokeAsync(StateHasChanged);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
    }

    private void OnLoad()
    {
        _loaded = true;
        _failed = false;
    }

    private void OnError()
    {
        _failed = true;
        _loaded = false;
    }
}
