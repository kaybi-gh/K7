using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class PgsSubtitleLoaderTests
{
    private List<(double Position, double Duration)> _requests = null!;
    private DateTime _now;

    [SetUp]
    public void SetUp()
    {
        _requests = [];
        _now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    [Test]
    public void Update_ShouldLoadAWindowStartingJustBehindThePlayhead()
    {
        using var loader = CreateLoader(_ => new PgsStreamBuilder().Show(52).Clear(55).ToArray());

        loader.Update(50);

        _requests.Should().Equal((50 - PgsSubtitleLoader.LookBehindSeconds, PgsSubtitleLoader.WindowSeconds));
        loader.FrameAt(53)!.TimeSeconds.Should().Be(52);
        loader.FrameAt(56).Should().BeNull();
    }

    [Test]
    public void Update_ShouldNotAskAgain_WhilePlayheadStaysFarFromTheWindowEnd()
    {
        using var loader = CreateLoader(_ => []);

        loader.Update(50);
        loader.Update(60);
        loader.Update(80);

        _requests.Should().HaveCount(1);
    }

    [Test]
    public void Update_ShouldExtendTheRange_WhenPlayheadNearsTheWindowEnd()
    {
        using var loader = CreateLoader(position =>
            position < 100
                ? new PgsStreamBuilder().Show(60).ToArray()
                // Same epoch: the second window places the object the first one defined.
                : new PgsStreamBuilder()
                    .Composition(170, 1920, 1080, PgsStreamBuilder.NormalCase, 0, (1, 5, 5))
                    .End(170)
                    .ToArray());
        var resets = 0;
        loader.Reset += () => resets++;

        loader.Update(50);
        var firstEnd = 40 + PgsSubtitleLoader.WindowSeconds;
        loader.Update(firstEnd - PgsSubtitleLoader.PrefetchSeconds + 1);

        _requests.Should().HaveCount(2);
        _requests[1].Position.Should().Be(firstEnd);
        resets.Should().Be(1);
        loader.FrameAt(171)!.Objects.Should().ContainSingle();
    }

    [Test]
    public void Update_ShouldStartOver_WhenSeekLandsOutsideTheLoadedRange()
    {
        using var loader = CreateLoader(position =>
            new PgsStreamBuilder().Show(position + 12).ToArray());
        var resets = 0;
        loader.Reset += () => resets++;

        loader.Update(50);
        loader.Update(3000);

        resets.Should().Be(2);
        _requests.Should().HaveCount(2);
        _requests[1].Position.Should().Be(3000 - PgsSubtitleLoader.LookBehindSeconds);
        loader.FrameAt(52.5).Should().BeNull();
        loader.FrameAt(3003)!.TimeSeconds.Should().Be(3002);
    }

    [Test]
    public void Update_ShouldReportFramesAndLoadedRange_AsTheyAreParsed()
    {
        using var loader = CreateLoader(_ => new PgsStreamBuilder().Show(52).Clear(55).ToArray());
        var frames = new List<double>();
        var loaded = new List<double>();
        loader.FrameLoaded += frame => frames.Add(frame.TimeSeconds);
        loader.LoadedUntilChanged += loaded.Add;

        loader.Update(50);

        frames.Should().Equal(52, 55);
        loaded.Should().Equal(52, 55, 40 + PgsSubtitleLoader.WindowSeconds);
    }

    [Test]
    public void Update_ShouldGiveUp_WhenTheServerCannotServeTheTrack()
    {
        using var loader = new PgsSubtitleLoader((position, duration, _) =>
        {
            _requests.Add((position, duration));
            return Task.FromResult<Stream?>(null);
        });

        loader.Update(50);
        loader.Update(51);

        loader.IsUnavailable.Should().BeTrue();
        _requests.Should().HaveCount(1);
    }

    [Test]
    public void Update_ShouldRetryAfterADelay_WhenAWindowFails()
    {
        var fail = true;
        using var loader = new PgsSubtitleLoader(
            (position, duration, _) =>
            {
                _requests.Add((position, duration));
                if (fail)
                    throw new HttpRequestException("boom");

                return Task.FromResult<Stream?>(new MemoryStream(new PgsStreamBuilder().Show(52).ToArray()));
            },
            () => _now);

        loader.Update(50);
        loader.Update(50.2);
        _requests.Should().HaveCount(1);

        fail = false;
        _now += TimeSpan.FromSeconds(10);
        loader.Update(50.4);

        _requests.Should().HaveCount(2);
        loader.IsUnavailable.Should().BeFalse();
        loader.FrameAt(52.5).Should().NotBeNull();
    }

    [Test]
    public void Update_ShouldStop_AfterRepeatedFailures()
    {
        using var loader = new PgsSubtitleLoader(
            (position, duration, _) =>
            {
                _requests.Add((position, duration));
                throw new HttpRequestException("boom");
            },
            () => _now);

        for (var i = 0; i < PgsSubtitleLoader.MaxConsecutiveFailures + 3; i++)
        {
            loader.Update(50 + i);
            _now += TimeSpan.FromSeconds(10);
        }

        loader.IsUnavailable.Should().BeTrue();
        _requests.Should().HaveCount(PgsSubtitleLoader.MaxConsecutiveFailures);
    }

    [Test]
    public void Update_ShouldDoNothing_AfterDispose()
    {
        var loader = CreateLoader(_ => []);
        loader.Dispose();

        loader.Update(50);

        _requests.Should().BeEmpty();
    }

    private PgsSubtitleLoader CreateLoader(Func<double, byte[]> window) =>
        new(
            (position, duration, _) =>
            {
                _requests.Add((position, duration));
                return Task.FromResult<Stream?>(new MemoryStream(window(position)));
            },
            () => _now);
}
