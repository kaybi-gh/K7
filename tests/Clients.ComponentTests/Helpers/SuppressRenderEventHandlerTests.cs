using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SuppressRenderEventHandlerTests
{
    [Test]
    public async Task HandleEventAsync_ShouldInvokeCallback_WithoutThrowing()
    {
        var invoked = false;
        var handler = new SuppressRenderEventHandler();
        Func<object?, Task> work = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };
        var callback = new EventCallbackWorkItem(work);

        await handler.HandleEventAsync(callback, null);

        invoked.Should().BeTrue();
    }
}
