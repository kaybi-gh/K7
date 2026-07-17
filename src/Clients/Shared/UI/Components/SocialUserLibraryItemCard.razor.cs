using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class SocialUserLibraryItemCard
{
    [Parameter, EditorRequired] public MediaCardViewModel Model { get; set; } = default!;
    [Parameter, EditorRequired] public string Href { get; set; } = default!;
    [Parameter] public Guid? CoverPictureId { get; set; }
    [Parameter] public IReadOnlyList<SocialUserMediaCardDto> PreviewItems { get; set; } = [];
    [Parameter] public bool PreferPreviewWhenEmpty { get; set; }
    [Parameter] public string PlaceholderIcon { get; set; } = Phosphor.Image;
    [Parameter] public bool ShowCopyButton { get; set; }
    [Parameter] public string? CopyAriaLabel { get; set; }
    [Parameter] public EventCallback OnCopy { get; set; }

    private MediaCardViewModel _viewModel = default!;
    private IReadOnlyList<string> _displayUrls = [];

    protected override void OnParametersSet()
    {
        _displayUrls = BuildDisplayUrls();
        _viewModel = Model with { PictureUrl = null };
    }

    private IReadOnlyList<string> BuildDisplayUrls()
    {
        var coverUrl = ResolveCoverPictureUrl(CoverPictureId);
        if (coverUrl is not null)
            return [coverUrl];

        return SocialUserBrowseMappings.GetPreviewImageUrls(PreviewItems, ApiClient);
    }

    private string? ResolveCoverPictureUrl(Guid? coverPictureId)
    {
        if (coverPictureId is not Guid id)
            return null;

        return ApiClient.GetAbsoluteUri($"/api/metadata-pictures/{id}?size=Medium")?.AbsoluteUri;
    }
}
