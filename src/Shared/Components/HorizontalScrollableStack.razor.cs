using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MediaClient.Shared.Components;

public partial class HorizontalScrollableStack
{
    private ElementReference _splide;

    [Parameter]
    public string Title { get; set; } = "";

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public int Limit { get; set; } = 5;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JSRuntime.InvokeVoidAsync("HorizontalScrollableStack.Init", _splide, Limit);
    }
}