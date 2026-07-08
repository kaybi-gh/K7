namespace K7.Server.Application.Common.Interfaces;

public interface IBackgroundTaskExecutionContext
{
    bool IsCancelled { get; }

    string? CancellationDetails { get; }

    void Cancel(string details);

    void Reset();
}
