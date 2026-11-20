using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class ChromaprintFingerprintMatcherTests
{
    [Test]
    public void FindContiguousAtShift_ShouldBridgeNeighbouringRuns_WhenGapIsSmallRelativeToRuns()
    {
        // Two 10s matching runs (~81 points each) separated by ~4s of mismatch.
        // Without bridging, each run is kept separately; with bridging they merge past MinIntro.
        const int runPoints = 81; // ~10s
        const int gapPoints = 32; // ~4s
        var shared = BuildFingerprint(runPoints * 2 + gapPoints, seed: 1);
        var lhs = shared.ToArray();
        var rhs = shared.ToArray();

        // Punch a mismatch gap in the middle of rhs so FindContiguous sees two runs.
        for (var i = runPoints; i < runPoints + gapPoints; i++)
            rhs[i] = ~rhs[i];

        var match = ChromaprintFingerprintMatcher.FindContiguousAtShift(lhs, rhs, shift: 0);

        match.Should().NotBeNull();
        var duration = match!.Value.LhsEndSeconds - match.Value.LhsStartSeconds;
        duration.Should().BeGreaterThan(18);
    }

    [Test]
    public void FindBestMatches_ShouldKeepNeighbourMatch_WhenFartherEpisodeSharesMore()
    {
        // Episodes 0 and 1 share ~20s. Episode 4 shares ~45s with episode 0, but a valid
        // neighbour match stops the search before it gets that far.
        var near = BuildFingerprint(160, seed: 1); // ~20s
        var far = BuildFingerprint(200, seed: 2); // ~25s

        uint[][] fps =
        [
            Concat(near, far),
            Concat(near, BuildFingerprint(200, seed: 3)),
            BuildFingerprint(360, seed: 4),
            BuildFingerprint(360, seed: 5),
            Concat(near, far),
        ];

        var matches = ChromaprintFingerprintMatcher.FindBestMatches(
            fps,
            minDurationSeconds: 15,
            maxDurationSeconds: 120);

        matches.Should().ContainKey(0);
        var duration = matches[0].EndSeconds - matches[0].StartSeconds;
        duration.Should().BeApproximately(160 * ChromaprintFingerprintMatcher.SampleDuration, 1);
    }

    [Test]
    public void FindBestMatches_ShouldReachFartherEpisodes_WhenNeighboursShareNothing()
    {
        // The season alternates openings: episode 0 only matches episode 5.
        var shared = BuildFingerprint(200, seed: 1); // ~25s

        uint[][] fps =
        [
            Concat(shared, BuildFingerprint(100, seed: 10)),
            BuildFingerprint(300, seed: 11),
            BuildFingerprint(300, seed: 12),
            BuildFingerprint(300, seed: 13),
            BuildFingerprint(300, seed: 14),
            Concat(shared, BuildFingerprint(100, seed: 15)),
        ];

        var matches = ChromaprintFingerprintMatcher.FindBestMatches(
            fps,
            minDurationSeconds: 15,
            maxDurationSeconds: 120);

        matches.Keys.Should().BeEquivalentTo([0, 5]);
        matches[0].StartSeconds.Should().Be(0);
        (matches[0].EndSeconds - matches[0].StartSeconds).Should().BeGreaterThan(20);
    }

    [Test]
    public void FindBestMatches_ShouldOnlyResolveTargets_WhenTargetsAreGiven()
    {
        var shared = BuildFingerprint(200, seed: 1);

        uint[][] fps =
        [
            Concat(shared, BuildFingerprint(100, seed: 10)),
            Concat(shared, BuildFingerprint(100, seed: 11)),
            Concat(shared, BuildFingerprint(100, seed: 12)),
        ];

        var matches = ChromaprintFingerprintMatcher.FindBestMatches(
            fps,
            minDurationSeconds: 15,
            maxDurationSeconds: 120,
            targets: new HashSet<int> { 2 });

        matches.Keys.Should().BeEquivalentTo([2]);
    }

    [Test]
    public void FindBestMatches_ShouldKeepShortMatch_WhenEnoughEpisodesShareIt()
    {
        // A ~10s title card, below the 15s minimum, repeated by the whole season.
        var card = BuildFingerprint(81, seed: 1);

        var fps = Enumerable.Range(0, 5)
            .Select(i => Concat(
                BuildFingerprint(40, seed: 20 + i),
                Concat(card, BuildFingerprint(200, seed: 40 + i))))
            .ToArray();

        var matches = ChromaprintFingerprintMatcher.FindBestMatches(
            fps,
            minDurationSeconds: 15,
            maxDurationSeconds: 120,
            shortMinDurationSeconds: 8);

        matches.Should().HaveCount(5);
        var duration = matches[2].EndSeconds - matches[2].StartSeconds;
        duration.Should().BeInRange(8, 15);
        matches[2].StartSeconds.Should().BeApproximately(40 * ChromaprintFingerprintMatcher.SampleDuration, 0.5);
    }

    [Test]
    public void FindBestMatches_ShouldRejectShortMatch_WhenOnlyOneEpisodeSharesIt()
    {
        var card = BuildFingerprint(81, seed: 1);

        uint[][] fps =
        [
            Concat(card, BuildFingerprint(200, seed: 10)),
            Concat(card, BuildFingerprint(200, seed: 11)),
            BuildFingerprint(281, seed: 12),
            BuildFingerprint(281, seed: 13),
            BuildFingerprint(281, seed: 14),
        ];

        var matches = ChromaprintFingerprintMatcher.FindBestMatches(
            fps,
            minDurationSeconds: 15,
            maxDurationSeconds: 120,
            shortMinDurationSeconds: 8);

        matches.Should().BeEmpty();
    }

    [Test]
    public void FindBestMatches_ShouldRejectShortMatch_WhenShortMinimumIsNotSet()
    {
        var card = BuildFingerprint(81, seed: 1);

        var fps = Enumerable.Range(0, 5)
            .Select(i => Concat(card, BuildFingerprint(200, seed: 40 + i)))
            .ToArray();

        var matches = ChromaprintFingerprintMatcher.FindBestMatches(
            fps,
            minDurationSeconds: 15,
            maxDurationSeconds: 120);

        matches.Should().BeEmpty();
    }

    private static uint[] BuildFingerprint(int length, int seed)
    {
        // Seeded random points: two different seeds never match within the bit tolerance.
        var random = new Random(seed);
        var fp = new uint[length];
        for (var i = 0; i < length; i++)
            fp[i] = (uint)random.NextInt64(0, (long)uint.MaxValue + 1);
        return fp;
    }

    private static uint[] Concat(uint[] a, uint[] b)
    {
        var result = new uint[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, result, 0, a.Length * sizeof(uint));
        Buffer.BlockCopy(b, 0, result, a.Length * sizeof(uint), b.Length * sizeof(uint));
        return result;
    }
}
