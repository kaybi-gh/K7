using K7.Clients.Shared.Components;
using MudBlazor;
using MudBlazor.Services;

namespace K7.Clients.ComponentTests;

public class CustomMudProvidersTests
{
    [Test]
    public async Task Renders_MudBlazor_Providers()
    {
        var ctx = new BunitContext();

        try
        {
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.Services.AddMudServices();

            var cut = ctx.Render<CustomMudProviders>();

            cut.HasComponent<MudPopoverProvider>().Should().BeTrue();
            cut.HasComponent<MudDialogProvider>().Should().BeTrue();
            cut.HasComponent<MudSnackbarProvider>().Should().BeTrue();
        }
        finally
        {
            await ctx.DisposeAsync();
        }
    }
}
