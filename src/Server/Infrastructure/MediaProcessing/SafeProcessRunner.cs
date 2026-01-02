using System.Diagnostics;

namespace K7.Server.Infrastructure.MediaProcessing;

public class SafeProcessRunner
{
    public static async Task<int> RunAsync(
        string fileName,
        string arguments,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        Func<Process, Task>? onBeforeKill = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = onStdout != null,
                RedirectStandardError = onStderr != null,
                RedirectStandardInput = false,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var readerTasks = new List<Task>();

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process '{fileName}'.");
            }

            if (onStdout != null)
            {
                readerTasks.Add(Task.Run(async () =>
                {
                    while (true)
                    {
                        var line = await process.StandardOutput.ReadLineAsync(linkedCts.Token);
                        if (line == null)
                            break;
                        onStdout(line);
                    }
                }, linkedCts.Token));
            }

            if (onStderr != null)
            {
                readerTasks.Add(Task.Run(async () =>
                {
                    while (true)
                    {
                        var line = await process.StandardError.ReadLineAsync(linkedCts.Token);
                        if (line == null)
                            break;
                        onStderr(line);
                    }
                }, linkedCts.Token));
            }

            var waitForExitTask = process.WaitForExitAsync(linkedCts.Token);
            var timeoutTask = timeout.HasValue ? Task.Delay(timeout.Value, linkedCts.Token) : Task.Delay(Timeout.Infinite, linkedCts.Token);

            var completed = await Task.WhenAny(waitForExitTask, timeoutTask);

            if (completed == timeoutTask)
            {
                linkedCts.Cancel();

                if (onBeforeKill != null)
                {
                    try
                    {
                        await onBeforeKill(process);
                    }
                    catch { }
                    await Task.Delay(500, CancellationToken.None);
                }

                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { }

                await Task.WhenAll(readerTasks.Where(t => !t.IsCompleted));

                throw new TimeoutException($"Process '{fileName}' timed out after {timeout!.Value.TotalSeconds} seconds.");
            }

            await Task.WhenAll(readerTasks);

            await waitForExitTask;

            return process.ExitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { }

            throw;
        }
        finally
        {
            linkedCts.Cancel();
        }
    }
}
