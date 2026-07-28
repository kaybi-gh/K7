namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Tracks the cancellation sources of the tasks currently running in this process, so that an operator
/// cancelling a task actually stops the work instead of only changing a row.
/// </summary>
public interface IBackgroundTaskCancellationRegistry
{
    /// <summary>Registers the cancellation source of a task that is starting.</summary>
    /// <param name="taskId">Identifier of the task.</param>
    /// <param name="cancellationTokenSource">Source linked to the token handed to the handler.</param>
    void Register(Guid taskId, CancellationTokenSource cancellationTokenSource);

    /// <summary>Removes a task once it has stopped running.</summary>
    /// <param name="taskId">Identifier of the task.</param>
    void Unregister(Guid taskId);

    /// <summary>
    /// Requests cancellation of a running task.
    /// </summary>
    /// <param name="taskId">Identifier of the task.</param>
    /// <returns><see langword="true"/> if the task was running in this process and was signalled.</returns>
    bool TryCancel(Guid taskId);
}
