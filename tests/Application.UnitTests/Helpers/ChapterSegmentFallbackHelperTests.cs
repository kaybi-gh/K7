using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class ChapterSegmentFallbackHelperTests
{
    [Test]
    public void TryCreateSegment_ShouldMapOpeningChapter_WhenInFirstQuarter()
    {
        var chapter = new ChapterMarker
        {
            Title = "Opening",
            StartSeconds = 0,
            EndSeconds = 90
        };

        var ok = ChapterSegmentFallbackHelper.TryCreateSegment(
            chapter, episodeDurationSeconds: 1400, MediaSegmentType.Intro,
            out var start, out var end);

        ok.Should().BeTrue();
        start.Should().Be(0);
        end.Should().Be(90);
    }

    [Test]
    public void TryCreateSegment_ShouldMapEndingChapter_WhenInLastQuarter()
    {
        var chapter = new ChapterMarker
        {
            Title = "Ending",
            StartSeconds = 1300,
            EndSeconds = 1380
        };

        var ok = ChapterSegmentFallbackHelper.TryCreateSegment(
            chapter, episodeDurationSeconds: 1400, MediaSegmentType.Outro,
            out var start, out var end);

        ok.Should().BeTrue();
        start.Should().Be(1300);
        end.Should().Be(1380);
    }

    [Test]
    public void TryCreateSegment_ShouldRejectOpening_WhenTooLateInEpisode()
    {
        var chapter = new ChapterMarker
        {
            Title = "Opening",
            StartSeconds = 800,
            EndSeconds = 890
        };

        var ok = ChapterSegmentFallbackHelper.TryCreateSegment(
            chapter, episodeDurationSeconds: 1400, MediaSegmentType.Intro,
            out _, out _);

        ok.Should().BeFalse();
    }

    [TestCase("Intro", 0, 6)]
    [TestCase("5. Title Sequence", 221, 227)]
    [TestCase("Main Titles", 60, 100)]
    public void TryCreateSegment_ShouldAcceptShortNamedIntro(string title, double start, double end)
    {
        var chapter = new ChapterMarker { Title = title, StartSeconds = start, EndSeconds = end };

        var ok = ChapterSegmentFallbackHelper.TryCreateSegment(
            chapter, episodeDurationSeconds: 1700, MediaSegmentType.Intro,
            out _, out _);

        ok.Should().BeTrue();
    }

    [Test]
    public void TryCreateSegment_ShouldAcceptOpening_WhenPastFirstQuarterButInFirstHalf()
    {
        var chapter = new ChapterMarker { Title = "Opening", StartSeconds = 416, EndSeconds = 505 };

        var ok = ChapterSegmentFallbackHelper.TryCreateSegment(
            chapter, episodeDurationSeconds: 1412, MediaSegmentType.Intro,
            out _, out _);

        ok.Should().BeTrue();
    }

    [Test]
    public void TryCreateSegment_ShouldRejectIntro_WhenShorterThanMinimum()
    {
        var chapter = new ChapterMarker { Title = "Intro", StartSeconds = 329, EndSeconds = 331 };

        var ok = ChapterSegmentFallbackHelper.TryCreateSegment(
            chapter, episodeDurationSeconds: 2218, MediaSegmentType.Intro,
            out _, out _);

        ok.Should().BeFalse();
    }

    [TestCase("Post Intro", MediaSegmentType.Intro, 331, 400)]
    [TestCase("Skip Intro", MediaSegmentType.Intro, 329, 340)]
    [TestCase("Post-Credits", MediaSegmentType.Outro, 1421, 1469)]
    [TestCase("Mid-Credits Scene", MediaSegmentType.Outro, 1400, 1440)]
    [TestCase("Credits End", MediaSegmentType.Outro, 1400, 1440)]
    public void TryCreateSegment_ShouldRejectMarkersAroundTheSegment(
        string title, MediaSegmentType type, double start, double end)
    {
        var chapter = new ChapterMarker { Title = title, StartSeconds = start, EndSeconds = end };

        var ok = ChapterSegmentFallbackHelper.TryCreateSegment(
            chapter, episodeDurationSeconds: 1469, type,
            out _, out _);

        ok.Should().BeFalse();
    }

    [Test]
    public void TryCreateSegment_ShouldNotMapOpeningCredits_AsOutro()
    {
        var chapter = new ChapterMarker { Title = "Opening Credits", StartSeconds = 1300, EndSeconds = 1380 };

        var ok = ChapterSegmentFallbackHelper.TryCreateSegment(
            chapter, episodeDurationSeconds: 1400, MediaSegmentType.Outro,
            out _, out _);

        ok.Should().BeFalse();
    }

    [Test]
    public void TryCreateSegment_ShouldMapShortEndCredits_AsOutro()
    {
        var chapter = new ChapterMarker { Title = "End Credits", StartSeconds = 1237, EndSeconds = 1246 };

        var ok = ChapterSegmentFallbackHelper.TryCreateSegment(
            chapter, episodeDurationSeconds: 1300, MediaSegmentType.Outro,
            out var start, out var end);

        ok.Should().BeTrue();
        start.Should().Be(1237);
        end.Should().Be(1246);
    }

    [Test]
    public void TryCreateSegment_ShouldAcceptFrenchGeneriqueFin_AsOutro()
    {
        var chapter = new ChapterMarker
        {
            Title = "Générique de fin",
            StartSeconds = 1320,
            EndSeconds = 1390
        };

        var ok = ChapterSegmentFallbackHelper.TryCreateSegment(
            chapter, episodeDurationSeconds: 1400, MediaSegmentType.Outro,
            out _, out _);

        ok.Should().BeTrue();
    }
}
