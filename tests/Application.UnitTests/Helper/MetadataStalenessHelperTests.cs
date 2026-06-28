using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helper;

public class MetadataStalenessHelperTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);

    [TestCase(null)]
    [TestCase(0)]
    [TestCase(-1)]
    public void IsStale_ShouldReturnFalse_WhenAutoRefreshIsNotConfigured(int? refreshIntervalDays)
    {
        MetadataStalenessHelper.IsStale(null, refreshIntervalDays, Now).Should().BeFalse();
        MetadataStalenessHelper.IsStale(Now.AddYears(-5), refreshIntervalDays, Now).Should().BeFalse();
    }

    [Test]
    public void IsStale_ShouldReturnTrue_WhenNeverRefreshedAndAutoRefreshIsConfigured()
    {
        MetadataStalenessHelper.IsStale(null, 30, Now).Should().BeTrue();
    }

    [Test]
    public void IsStale_ShouldReturnTrue_WhenLastRefreshIsOlderThanInterval()
    {
        var lastRefresh = Now.AddDays(-31);
        MetadataStalenessHelper.IsStale(lastRefresh, 30, Now).Should().BeTrue();
    }

    [Test]
    public void IsStale_ShouldReturnFalse_WhenLastRefreshIsWithinInterval()
    {
        var lastRefresh = Now.AddDays(-10);
        MetadataStalenessHelper.IsStale(lastRefresh, 30, Now).Should().BeFalse();
    }
}
