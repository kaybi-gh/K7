using System.Collections;
using System.Reflection;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7DialogHostTests
{
    [Test]
    public async Task Close_ShouldReleaseTheDialogEntry_WhenDialogIsClosed()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddLogging();
        var dialogService = new K7DialogService();
        ctx.Services.AddSingleton<IK7DialogService>(dialogService);
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());
        ctx.Services.AddSingleton(Substitute.For<IClientErrorReporter>());

        var cut = ctx.Render<K7DialogHost>();

        var reference = await cut.InvokeAsync(() => dialogService.ShowAsync<EmptyDialog>("Title"));
        TrackedDialogs(cut.Instance).Should().Be(1);

        await cut.InvokeAsync(() => reference.Close(K7DialogResult.Ok()));

        cut.WaitForAssertion(() => TrackedDialogs(cut.Instance).Should().Be(0));
        (await reference.Result).Canceled.Should().BeFalse();
    }

    private static int TrackedDialogs(K7DialogHost host) =>
        ((ICollection)typeof(K7DialogHost)
            .GetField("_dialogs", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(host)!).Count;

    private sealed class EmptyDialog : ComponentBase
    {
    }
}
