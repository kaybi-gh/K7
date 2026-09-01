using K7.Server.Web.Infrastructure;
using K7.Server.Web.Middleware;

namespace K7.Server.Web.SmokeTests;

[TestFixture]
public class SetupRequiredMiddlewareTests
{
    [TestCase(HealthProbePaths.Liveness)]
    [TestCase(HealthProbePaths.Readiness)]
    [TestCase("/setup")]
    [TestCase("/api/setup")]
    public void IsAllowedDuringSetup_ShouldAllowHealthAndSetupPaths(string path)
    {
        SetupRequiredMiddleware.IsAllowedDuringSetup(path).Should().BeTrue();
    }

    [Test]
    public void IsAllowedDuringSetup_ShouldRejectOrdinaryApi()
    {
        SetupRequiredMiddleware.IsAllowedDuringSetup("/api/medias").Should().BeFalse();
    }
}

[TestFixture]
public class HealthProbePathsTests
{
    [Test]
    public void Liveness_ShouldBeDistinctFromReadiness()
    {
        HealthProbePaths.Liveness.Should().Be("/alive");
        HealthProbePaths.Readiness.Should().Be("/health");
        HealthProbePaths.Liveness.Should().NotBe(HealthProbePaths.Readiness);
        HealthProbePaths.LiveTag.Should().Be("live");
    }
}
