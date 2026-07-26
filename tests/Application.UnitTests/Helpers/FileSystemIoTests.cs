using FluentAssertions;
using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

public class FileSystemIoTests
{
    [Test]
    public void Run_ShouldReturnResult_WhenWorkCompletesWithinTimeout()
    {
        var result = FileSystemIo.Run(() => 42, TimeSpan.FromSeconds(1));

        result.Should().Be(42);
    }

    [Test]
    public void Run_ShouldThrowTimeoutException_WhenWorkExceedsTimeout()
    {
        var act = () => FileSystemIo.Run(
            () =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
                return 1;
            },
            TimeSpan.FromMilliseconds(50));

        act.Should().Throw<TimeoutException>();
    }
}
