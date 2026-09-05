using AwesomeAssertions;
using K7.Server.Application.Common;
using K7.Server.Application.Services;

namespace K7.Server.Application.UnitTests.Services;

public class MetadataProviderCooldownStoreTests
{
    [Test]
    public void Report_ShouldMarkProviderCoolingDown_UntilRetryAfter()
    {
        var store = new MetadataProviderCooldownStore();
        var now = DateTimeOffset.Parse("2026-08-02T10:00:00Z");

        store.Report(MetadataProviderNames.Tmdb, TimeSpan.FromMinutes(2), now);

        store.IsCoolingDown(MetadataProviderNames.Tmdb, now).Should().BeTrue();
        store.GetCooldownUntil(MetadataProviderNames.Tmdb, now).Should().Be(now.AddMinutes(2));
        store.IsCoolingDown(MetadataProviderNames.Tmdb, now.AddMinutes(2)).Should().BeFalse();
    }

    [Test]
    public void Report_ShouldKeepLaterCooldown_WhenShorterReportArrives()
    {
        var store = new MetadataProviderCooldownStore();
        var now = DateTimeOffset.Parse("2026-08-02T10:00:00Z");

        store.Report(MetadataProviderNames.Tvdb, TimeSpan.FromMinutes(5), now);
        store.Report(MetadataProviderNames.Tvdb, TimeSpan.FromMinutes(1), now);

        store.GetCooldownUntil(MetadataProviderNames.Tvdb, now).Should().Be(now.AddMinutes(5));
    }

    [Test]
    public void GetCoolingDownProviders_ShouldExcludeExpiredEntries()
    {
        var store = new MetadataProviderCooldownStore();
        var now = DateTimeOffset.Parse("2026-08-02T10:00:00Z");

        store.Report(MetadataProviderNames.Tmdb, TimeSpan.FromMinutes(1), now);
        store.Report(MetadataProviderNames.Tvdb, TimeSpan.FromMinutes(5), now);

        var cooling = store.GetCoolingDownProviders(now.AddMinutes(2));
        cooling.Should().Equal(MetadataProviderNames.Tvdb);
    }
}
