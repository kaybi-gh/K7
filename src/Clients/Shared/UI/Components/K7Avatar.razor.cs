using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Avatar
{
    private static readonly string[] Palette =
    [
        "#e57373", "#f06292", "#ba68c8", "#9575cd",
        "#7986cb", "#64b5f6", "#4fc3f7", "#4dd0e1",
        "#4db6ac", "#81c784", "#aed581", "#ffb74d",
        "#ff8a65", "#a1887f", "#90a4ae", "#7c4dff"
    ];

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Image { get; set; } = "";
    [Parameter] public string Alt { get; set; } = "";
    [Parameter] public string Size { get; set; } = "";
    [Parameter] public string Color { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public string Letter { get; set; } = "";
    [Parameter] public Guid? UserId { get; set; }

    [Inject] private IK7ServerService ApiClient { get; set; } = default!;

    private bool _imageFailed;
    private string? _previousImage;

    protected override void OnParametersSet()
    {
        if (!string.Equals(_previousImage, Image, StringComparison.Ordinal))
        {
            _previousImage = Image;
            _imageFailed = false;
        }
    }

    private string? ResolvedImage
    {
        get
        {
            if (string.IsNullOrEmpty(Image) || _imageFailed)
                return null;

            if (Image.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || Image.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return Image;

            return ApiClient.GetAbsoluteUri(Image)?.AbsoluteUri ?? Image;
        }
    }

    private void OnImageError() => _imageFailed = true;

    private string DisplayLetter
    {
        get
        {
            if (!string.IsNullOrEmpty(Letter))
                return Letter;

            if (string.IsNullOrWhiteSpace(Alt))
                return "";

            return char.ToUpperInvariant(Alt.Trim()[0]).ToString();
        }
    }

    private string? ComputedStyle
    {
        get
        {
            if (!string.IsNullOrEmpty(ResolvedImage) || string.IsNullOrEmpty(DisplayLetter))
                return string.IsNullOrEmpty(Style) ? null : Style;

            var bgColor = UserId is not null ? GetColorForUser(UserId.Value) : null;
            var baseStyle = bgColor is not null
                ? $"background-color:{bgColor};color:#fff;"
                : "background-color:var(--color-accent);color:var(--color-accent-fg);";
            return string.IsNullOrEmpty(Style) ? baseStyle : $"{baseStyle}{Style}";
        }
    }

    private static string GetColorForUser(Guid userId)
    {
        var hash = Math.Abs(userId.GetHashCode());
        return Palette[hash % Palette.Length];
    }
}
