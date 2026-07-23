using K7.Clients.Shared.Services;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class AmbientThemeRoutesTests
{
    private static readonly Guid MediaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PersonId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Test]
    public void TryGetThemeMediaId_ShouldParseMovieRoute()
    {
        var ok = AmbientThemeRoutes.TryGetThemeMediaId($"https://app/movies/{MediaId}", out var id);

        ok.Should().BeTrue();
        id.Should().Be(MediaId);
    }

    [Test]
    public void TryGetThemeMediaId_ShouldParseSerieRoute()
    {
        var ok = AmbientThemeRoutes.TryGetThemeMediaId($"https://app/series/{MediaId}", out var id);

        ok.Should().BeTrue();
        id.Should().Be(MediaId);
    }

    [Test]
    public void TryGetThemeMediaId_ShouldParseSerieSeasonRoute()
    {
        var ok = AmbientThemeRoutes.TryGetThemeMediaId($"https://app/series/{MediaId}/seasons/2", out var id);

        ok.Should().BeTrue();
        id.Should().Be(MediaId);
    }

    [Test]
    public void TryGetThemeMediaId_ShouldParseSerieEpisodeRoute()
    {
        var ok = AmbientThemeRoutes.TryGetThemeMediaId(
            $"https://app/series/{MediaId}/seasons/1/episodes/3",
            out var id);

        ok.Should().BeTrue();
        id.Should().Be(MediaId);
    }

    [Test]
    public void TryGetThemeMediaId_ShouldRejectPersonRoute()
    {
        var ok = AmbientThemeRoutes.TryGetThemeMediaId($"https://app/persons/{PersonId}", out _);

        ok.Should().BeFalse();
    }

    [Test]
    public void TryGetThemeMediaId_ShouldRejectHomeRoute()
    {
        AmbientThemeRoutes.TryGetThemeMediaId("https://app/", out _).Should().BeFalse();
        AmbientThemeRoutes.TryGetThemeMediaId("https://app/explore", out _).Should().BeFalse();
    }

    [Test]
    public void IsPersonRoute_ShouldMatchPersonDetail()
    {
        AmbientThemeRoutes.IsPersonRoute($"https://app/persons/{PersonId}").Should().BeTrue();
    }

    [Test]
    public void IsPersonRoute_ShouldRejectMediaRoutes()
    {
        AmbientThemeRoutes.IsPersonRoute($"https://app/series/{MediaId}").Should().BeFalse();
        AmbientThemeRoutes.IsPersonRoute($"https://app/movies/{MediaId}").Should().BeFalse();
    }

    [Test]
    public void IsThemeHoldingRoute_ShouldIncludeMediaTreeAndPersons()
    {
        AmbientThemeRoutes.IsThemeHoldingRoute($"https://app/series/{MediaId}").Should().BeTrue();
        AmbientThemeRoutes.IsThemeHoldingRoute($"https://app/series/{MediaId}/seasons/1").Should().BeTrue();
        AmbientThemeRoutes.IsThemeHoldingRoute($"https://app/series/{MediaId}/seasons/1/episodes/2").Should().BeTrue();
        AmbientThemeRoutes.IsThemeHoldingRoute($"https://app/movies/{MediaId}").Should().BeTrue();
        AmbientThemeRoutes.IsThemeHoldingRoute($"https://app/persons/{PersonId}").Should().BeTrue();
    }

    [Test]
    public void IsThemeHoldingRoute_ShouldExcludeUnrelatedPages()
    {
        AmbientThemeRoutes.IsThemeHoldingRoute("https://app/").Should().BeFalse();
        AmbientThemeRoutes.IsThemeHoldingRoute("https://app/explore").Should().BeFalse();
        AmbientThemeRoutes.IsThemeHoldingRoute("https://app/settings").Should().BeFalse();
    }
}
