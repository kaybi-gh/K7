using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7CoverMosaic
{
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;

    [Parameter] public MetadataPictureDto? CoverPicture { get; set; }
    [Parameter] public IReadOnlyList<MetadataPictureDto>? PreviewPictures { get; set; }
    [Parameter] public IReadOnlyList<string>? ImageUrls { get; set; }
    [Parameter] public bool PreferPreviewWhenEmpty { get; set; }
    [Parameter] public string PlaceholderIcon { get; set; } = Phosphor.Image;
    [Parameter] public MetadataPictureSize PictureSize { get; set; } = MetadataPictureSize.Small;
    [Parameter] public string? Alt { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Style { get; set; }

    private enum DisplayMode { Placeholder, CustomCover, Mosaic }

    private DisplayMode _displayMode;
    private string? _customCoverUrl;
    private List<string> _mosaicUrls = [];

    protected override void OnParametersSet()
    {
        _mosaicUrls = ResolveMosaicUrls();
        _customCoverUrl = ResolveUrl(CoverPicture);

        if (!PreferPreviewWhenEmpty && _customCoverUrl is not null)
        {
            _displayMode = DisplayMode.CustomCover;
            return;
        }

        if (_mosaicUrls.Count > 0)
        {
            _displayMode = DisplayMode.Mosaic;
            return;
        }

        if (_customCoverUrl is not null)
        {
            _displayMode = DisplayMode.CustomCover;
            return;
        }

        _displayMode = DisplayMode.Placeholder;
    }

    private List<string> ResolveMosaicUrls()
    {
        if (ImageUrls is { Count: > 0 })
            return ImageUrls.Where(url => !string.IsNullOrEmpty(url)).Take(4).ToList();

        if (PreviewPictures is not { Count: > 0 })
            return [];

        return PreviewPictures
            .Select(ResolveUrl)
            .Where(url => url is not null)
            .Cast<string>()
            .Take(4)
            .ToList();
    }

    private string? ResolveUrl(MetadataPictureDto? picture)
    {
        var uri = picture?.GetUri(PictureSize)?.OriginalString;
        return ApiClient.GetAbsoluteUri(uri)?.AbsoluteUri;
    }

    private static int GetGridVariant(int count) => count switch
    {
        1 => 1,
        2 => 2,
        3 => 3,
        _ => 4
    };

    private static string GetTileStyle(string url) =>
        $"background-image: url('{url.Replace("'", "%27")}')";
}
