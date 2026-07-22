using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components;

public enum K7ImageLoadingMode
{
    /// <summary>Blazor spinner + @onload re-render (heroes / detail).</summary>
    Interactive,

    /// <summary>
    /// CSS spinner stays in the DOM; native onload toggles a class.
    /// No Blazor re-render per image - safe for dense grids on TV.
    /// </summary>
    Css,

    /// <summary>No loading indicator.</summary>
    None
}

public partial class K7Image
{
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
    [Parameter] public K7ImageLoadingMode? LoadingMode { get; set; }
    [Parameter] public string FallbackIcon { get; set; } = Phosphor.ImageBroken;
    [Parameter] public RenderFragment? FallbackContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _imgRef;
    private string? _trackedSrc;
    private bool _loaded;
    private bool _failed;

    private K7ImageLoadingMode EffectiveLoadingMode =>
        LoadingMode ?? (ShowLoadingState ? K7ImageLoadingMode.Interactive : K7ImageLoadingMode.None);

    private bool HasSource => !string.IsNullOrEmpty(Src);
    private bool HasCustomFallback => FallbackContent is not null;
    private bool ShowFallback => !HasSource || _failed;
    private bool UseInteractiveLoading => EffectiveLoadingMode == K7ImageLoadingMode.Interactive;
    private bool UseCssLoading => EffectiveLoadingMode == K7ImageLoadingMode.Css;
    private bool ShowInteractiveSpinner => HasSource && !_loaded && !_failed && UseInteractiveLoading;
    private bool ShowCssSpinner => HasSource && !_failed && UseCssLoading;
    private bool UseLoadTransition => HasSource && !_failed && UseInteractiveLoading;

    private string WrapCssClass =>
        string.Join(" ",
            new[]
            {
                "k7-img-wrap",
                Fluid ? "k7-img-wrap--fluid" : null,
                ShowFallback && HasCustomFallback ? "k7-img-wrap--custom-fallback" : null,
                UseCssLoading ? "k7-img-wrap--css-loading" : null,
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
            if (UseCssLoading)
            {
                // Native handler - no Blazor event / StateHasChanged.
                attributes["onload"] = "this.classList.add('k7-img--loaded')";
            }

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
