using K7.Clients.Shared.Interfaces;

namespace K7.Clients.MAUI.Services;

public sealed class MauiUiDispatcher : IUiDispatcher
{
    public ValueTask InvokeAsync(Func<Task> work)
    {
        if (MainThread.IsMainThread)
            return new ValueTask(work());

        return new ValueTask(MainThread.InvokeOnMainThreadAsync(work));
    }
}
