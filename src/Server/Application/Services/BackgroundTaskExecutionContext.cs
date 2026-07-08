using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Services;

public sealed class BackgroundTaskExecutionContext : IBackgroundTaskExecutionContext
{
    public bool IsCancelled { get; private set; }

    public string? CancellationDetails { get; private set; }

    public void Cancel(string details)
    {
        IsCancelled = true;
        CancellationDetails = details;
    }

    public void Reset()
    {
        IsCancelled = false;
        CancellationDetails = null;
    }
}
