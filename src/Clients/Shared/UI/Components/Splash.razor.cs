using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class Splash
{
    private ElementReference _animationContainer;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await JS.InvokeVoidAsync("K7.Lottie.play", _animationContainer,
                    "_content/K7.Clients.Shared.UI/animations/splash.json");
            }
            catch (JSException) { }
        }
    }
}
