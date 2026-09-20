namespace K7.Clients.Shared.Interfaces;

public interface IUiDispatcher
{
    ValueTask InvokeAsync(Func<Task> work);
}
