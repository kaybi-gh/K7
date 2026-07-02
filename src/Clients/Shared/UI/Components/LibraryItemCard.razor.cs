using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components;

public partial class LibraryItemCard
{
    [Parameter, EditorRequired] public required string Title { get; set; }
    [Parameter] public string? Subtitle { get; set; }
    [Parameter] public MetadataPictureDto? CoverPicture { get; set; }
    [Parameter] public IReadOnlyList<MetadataPictureDto>? PreviewPictures { get; set; }
    [Parameter] public IReadOnlyList<string>? ImageUrls { get; set; }
    [Parameter] public bool PreferPreviewWhenEmpty { get; set; }
    [Parameter] public string PlaceholderIcon { get; set; } = Phosphor.Image;
    [Parameter] public MetadataPictureSize PictureSize { get; set; } = MetadataPictureSize.Small;
    [Parameter] public RenderFragment? HeaderEndContent { get; set; }
    [Parameter] public RenderFragment? FooterContent { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Style { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private async Task HandleClick()
    {
        if (OnClick.HasDelegate)
            await OnClick.InvokeAsync();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!OnClick.HasDelegate)
            return;

        if (e.Key is "Enter" or " ")
            await OnClick.InvokeAsync();
    }
}
