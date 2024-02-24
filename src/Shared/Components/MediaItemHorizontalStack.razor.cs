using MediaClient.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MediaClient.Shared.Components;

public partial class MediaItemHorizontalStack
{
    private Guid id = Guid.NewGuid();
    private ElementReference _splide;

    [Parameter]
    public string Title { get; set; } = "";

    [Parameter]
    public required List<MediaItem> MediaItems { get; set; } = new();

    [Parameter]
    public int Limit { get; set; } = 5;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JSRuntime.InvokeVoidAsync("MediaItemHorizontalStack.Init", _splide, Limit);
        }
    }
}