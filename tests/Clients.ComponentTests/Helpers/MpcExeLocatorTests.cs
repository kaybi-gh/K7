using AwesomeAssertions;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MpcExeLocatorTests
{
    [Test]
    public void EnumerateCandidates_ShouldIncludeCommonInstallNames()
    {
        var candidates = MpcExeLocator.EnumerateCandidates();

        candidates.Should().Contain(path => path.EndsWith("mpc-hc64.exe", StringComparison.OrdinalIgnoreCase));
        candidates.Should().Contain(path => path.EndsWith("mpc-be64.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void TryFind_ShouldReturnFirstExistingCandidate()
    {
        var existing = MpcExeLocator.EnumerateCandidates()[3];

        var found = MpcExeLocator.TryFind(path => path == existing);

        found.Should().Be(existing);
    }

    [Test]
    public void TryFind_ShouldReturnNull_WhenNoneExist()
    {
        MpcExeLocator.TryFind(_ => false).Should().BeNull();
    }
}
