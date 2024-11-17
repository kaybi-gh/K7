using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Security.Cryptography;

namespace K7.Clients.Shared.Components;

public partial class VideoPlayer
{
    private ElementReference _player;

    [Parameter]
    public List<string> Sources { get; set; } = [];

    [Parameter]
    public bool Visible { get; set; }

    //protected override async Task OnAfterRenderAsync(bool firstRender)
    //{
    //    if (firstRender && PlayerService.IsVisible)
    //    {
    //        //await JSRuntime.InvokeVoidAsync("initPlayer", _id, DotNetObjectReference.Create(this));
    //    }
    //}
    protected override void OnInitialized()
    {
        PlayerService.SourcesOnChange += OnSourceChange;
        PlayerService.IsVisibleOnChange += StateHasChanged;
    }

    public void Dispose()
    {
        PlayerService.SourcesOnChange -= OnSourceChange;
        PlayerService.IsVisibleOnChange -= StateHasChanged;
        PlayerService.PosterOnChange -= StateHasChanged;
    }

    private void OnSourceChange()
    {
        StateHasChanged();
        JSRuntime.InvokeVoidAsync("playerInterop.logAudioTracksLength", _player);
    }
}
