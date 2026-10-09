using AwesomeAssertions;
using K7.Clients.Shared.Models;

namespace K7.Clients.ComponentTests.Models;

[TestFixture]
public class PlayerSourceTests
{
    [Test]
    public void TryConsumePendingSeek_ShouldApply_WhenPositionIsPastOneSecond()
    {
        var source = new PlayerSource { PendingSeekTime = 42 };

        source.TryConsumePendingSeek(out var seconds).Should().BeTrue();

        seconds.Should().Be(42);
        source.PendingSeekTime.Should().BeNull();
    }

    [Test]
    public void TryConsumePendingSeek_ShouldLeaveThePosition_WhenItIsAtMostOneSecond()
    {
        var source = new PlayerSource { PendingSeekTime = 1 };

        source.TryConsumePendingSeek(out var seconds).Should().BeFalse();

        seconds.Should().Be(0);
        source.PendingSeekTime.Should().Be(1);
    }
}
