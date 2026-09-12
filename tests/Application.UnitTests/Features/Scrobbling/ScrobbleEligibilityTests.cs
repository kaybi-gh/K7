using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Features.Scrobbling;

[TestFixture]
public class ScrobbleEligibilityTests
{
    [Test]
    public void MeetsListenThreshold_ShouldBeTrue_WhenHalfway()
    {
        ScrobbleEligibility.MeetsListenThreshold(50, 600).Should().BeTrue();
        ScrobbleEligibility.MeetsListenThreshold(49, 400).Should().BeFalse();
    }

    [Test]
    public void MeetsListenThreshold_ShouldBeTrue_WhenFourMinutesElapsed()
    {
        ScrobbleEligibility.MeetsListenThreshold(20, 1300).Should().BeTrue();
        ScrobbleEligibility.MeetsListenThreshold(10, 600).Should().BeFalse();
    }

    [Test]
    public void AllowsMediaType_ShouldAllowAll_WhenEmpty()
    {
        ScrobbleEligibility.AllowsMediaType([], MediaType.Movie).Should().BeTrue();
    }

    [Test]
    public void AllowsMediaType_ShouldRespectConfiguredTypes()
    {
        ScrobbleEligibility.AllowsMediaType([MediaType.MusicTrack], MediaType.Movie).Should().BeFalse();
        ScrobbleEligibility.AllowsMediaType([MediaType.MusicTrack], MediaType.MusicTrack).Should().BeTrue();
    }

    [Test]
    public void WebhookPresets_ShouldIncludeYamtrackAndBetaSeries()
    {
        ScrobbleWebhookPresets.All.Should().Contain(p => p.Id == "yamtrack");
        ScrobbleWebhookPresets.All.Should().Contain(p => p.Id == "betaseries");
        ScrobbleWebhookPresets.All.Should().Contain(p => p.Id == "custom");
    }

    [Test]
    public void BetaSeriesPayload_ShouldBeFormInnerJson_WithMovieImdb()
    {
        var payload = ScrobblePlexGuids.CreateBetaSeriesTestSample(Guid.NewGuid());
        var json = ScrobblePlexGuids.BuildFormPayloadJson(payload, "media.scrobble");

        json.Should().Contain("\"event\":\"media.scrobble\"");
        json.Should().Contain("\"type\":\"movie\"");
        json.Should().Contain("imdb://tt0137523");
        json.Should().Contain("com.plexapp.agents.imdb://tt0137523");
        json.Should().NotContain("\"payload\"");
        ScrobblePlexGuids.HasRequiredIds(payload).Should().BeTrue();
    }

    [Test]
    public void BetaSeriesPayload_ShouldRequireTvdb_ForEpisodes()
    {
        var payload = ScrobblePlexGuids.CreateBetaSeriesTestSample(Guid.NewGuid()) with
        {
            MediaType = MediaType.SerieEpisode,
            Imdb = null,
            Tvdb = null,
            ShowName = "Show",
            SeasonNumber = 1,
            EpisodeNumber = 2
        };

        ScrobblePlexGuids.HasRequiredIds(payload).Should().BeFalse();

        var withTvdb = payload with { Tvdb = "12345" };
        ScrobblePlexGuids.HasRequiredIds(withTvdb).Should().BeTrue();
        var json = ScrobblePlexGuids.BuildFormPayloadJson(withTvdb, "media.scrobble");
        json.Should().Contain("\"type\":\"episode\"");
        json.Should().Contain("tvdb://12345");
    }
}
