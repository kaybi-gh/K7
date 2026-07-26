namespace K7.Server.Application.Helpers;

/// <summary>
/// Runs filesystem work with a wall-clock timeout so a hung SMB/NAS call cannot block indexing forever.
/// Timed-out work may still occupy a thread-pool thread until the OS unblocks.
/// </summary>
public static class FileSystemIo
{
    public static readonly TimeSpan DirectoryEnumerationTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan FileAccessTimeout = TimeSpan.FromSeconds(15);

    public static T Run<T>(Func<T> work, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var task = Task.Run(work, cancellationToken);
        try
        {
            return task.WaitAsync(timeout, cancellationToken).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            // Leave task running; caller decides how to continue.
            throw;
        }
    }

    public static void Run(Action work, TimeSpan timeout, CancellationToken cancellationToken = default)
        => Run(() =>
        {
            work();
            return true;
        }, timeout, cancellationToken);
}
