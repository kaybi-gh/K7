namespace K7.Clients.Shared.UI.Helpers;

public sealed class DebouncedActionRunner : IDisposable
{
    private readonly Func<Task> _action;
    private readonly Func<Func<Task>, Task> _invokeAsync;
    private readonly int _delayMs;
    private CancellationTokenSource? _cts;

    public DebouncedActionRunner(Func<Task> action, Func<Func<Task>, Task> invokeAsync, int delayMs = 500)
    {
        _action = action;
        _invokeAsync = invokeAsync;
        _delayMs = delayMs;
    }

    public void Schedule()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = RunAsync(token);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_delayMs, cancellationToken);
            await _invokeAsync(_action);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
