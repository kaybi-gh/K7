using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public async ValueTask InvokeAsync(Func<Task> work) => await work();
}
