using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class DebouncedActionRunnerTests
{
    [Test]
    public async Task Schedule_ShouldRunOnce_WhenCalledRapidly()
    {
        var runs = 0;
        using var runner = new DebouncedActionRunner(
            () =>
            {
                runs++;
                return Task.CompletedTask;
            },
            action => action(),
            delayMs: 40);

        runner.Schedule();
        runner.Schedule();
        runner.Schedule();

        await Task.Delay(120);

        runs.Should().Be(1);
    }

    [Test]
    public async Task Schedule_ShouldNotRun_WhenDisposedDuringDelay()
    {
        var runs = 0;
        var runner = new DebouncedActionRunner(
            () =>
            {
                runs++;
                return Task.CompletedTask;
            },
            action => action(),
            delayMs: 80);

        runner.Schedule();
        runner.Dispose();
        await Task.Delay(120);

        runs.Should().Be(0);
    }
}
