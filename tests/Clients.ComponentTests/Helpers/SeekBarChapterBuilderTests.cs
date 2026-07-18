using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SeekBarChapterBuilderTests
{
    [Test]
    public void Build_ShouldReturnEmpty_WhenChapterTicksDisabled()
    {
        var chapters = new[] { new ChapterMarkerDto { StartSeconds = 0, Title = "Opening" } };
        var segments = new[]
        {
            new MediaSegmentDto { Type = MediaSegmentType.Intro, StartMs = 0, EndMs = 90_000 }
        };

        var result = SeekBarChapterBuilder.Build(false, chapters, segments, "Intro", "Outro");

        result.Should().BeEmpty();
    }

    [Test]
    public void Build_ShouldUseIntroOutro_WhenNoFileChapters()
    {
        var segments = new[]
        {
            new MediaSegmentDto { Type = MediaSegmentType.Intro, StartMs = 0, EndMs = 90_000 },
            new MediaSegmentDto { Type = MediaSegmentType.Outro, StartMs = 2_400_000, EndMs = 2_500_000 }
        };

        var result = SeekBarChapterBuilder.Build(true, null, segments, "Intro", "Outro");

        result.Should().BeEquivalentTo(
        [
            new SeekBarChapterBuilder.Marker("Intro", 0),
            new SeekBarChapterBuilder.Marker(null, 90),
            new SeekBarChapterBuilder.Marker("Outro", 2400),
            new SeekBarChapterBuilder.Marker(null, 2500)
        ], options => options.WithStrictOrdering());
    }

    [Test]
    public void Build_ShouldKeepFileChaptersOnly_WhenIntroOverlapsChapter()
    {
        var chapters = new[]
        {
            new ChapterMarkerDto { StartSeconds = 0, Title = "Opening" },
            new ChapterMarkerDto { StartSeconds = 120, Title = "Episode" }
        };
        var segments = new[]
        {
            new MediaSegmentDto { Type = MediaSegmentType.Intro, StartMs = 0, EndMs = 90_000 }
        };

        var result = SeekBarChapterBuilder.Build(true, chapters, segments, "Intro", "Outro");

        result.Should().BeEquivalentTo(
        [
            new SeekBarChapterBuilder.Marker("Opening", 0),
            new SeekBarChapterBuilder.Marker("Episode", 120)
        ], options => options.WithStrictOrdering());
    }

    [Test]
    public void Build_ShouldMergeOutro_WhenNoChapterNearOutro()
    {
        var chapters = new[]
        {
            new ChapterMarkerDto { StartSeconds = 0, Title = "Opening" },
            new ChapterMarkerDto { StartSeconds = 600, Title = "Mid" }
        };
        var segments = new[]
        {
            new MediaSegmentDto { Type = MediaSegmentType.Outro, StartMs = 2_400_000, EndMs = 2_500_000 }
        };

        var result = SeekBarChapterBuilder.Build(true, chapters, segments, "Intro", "Outro");

        result.Should().BeEquivalentTo(
        [
            new SeekBarChapterBuilder.Marker("Opening", 0),
            new SeekBarChapterBuilder.Marker("Mid", 600),
            new SeekBarChapterBuilder.Marker("Outro", 2400),
            new SeekBarChapterBuilder.Marker(null, 2500)
        ], options => options.WithStrictOrdering());
    }

    [Test]
    public void Build_ShouldSkipOutro_WhenChapterWithinToleranceOfOutroStart()
    {
        var chapters = new[]
        {
            new ChapterMarkerDto { StartSeconds = 2399, Title = "Credits" }
        };
        var segments = new[]
        {
            new MediaSegmentDto { Type = MediaSegmentType.Outro, StartMs = 2_400_000, EndMs = 2_500_000 }
        };

        var result = SeekBarChapterBuilder.Build(true, chapters, segments, "Intro", "Outro");

        result.Should().BeEquivalentTo(
        [
            new SeekBarChapterBuilder.Marker(null, 0),
            new SeekBarChapterBuilder.Marker("Credits", 2399)
        ], options => options.WithStrictOrdering());
    }

    [Test]
    public void Build_ShouldPrependOrigin_WhenIntroStartsAfterColdOpen()
    {
        var segments = new[]
        {
            new MediaSegmentDto { Type = MediaSegmentType.Intro, StartMs = 90_000, EndMs = 180_000 },
            new MediaSegmentDto { Type = MediaSegmentType.Outro, StartMs = 1_300_000, EndMs = 1_380_000 }
        };

        var result = SeekBarChapterBuilder.Build(true, null, segments, "Intro", "Outro");

        result.Should().BeEquivalentTo(
        [
            new SeekBarChapterBuilder.Marker(null, 0),
            new SeekBarChapterBuilder.Marker("Intro", 90),
            new SeekBarChapterBuilder.Marker(null, 180),
            new SeekBarChapterBuilder.Marker("Outro", 1300),
            new SeekBarChapterBuilder.Marker(null, 1380)
        ], options => options.WithStrictOrdering());
    }

    [Test]
    public void Build_ShouldReturnEmpty_WhenNoChaptersAndNoSegments()
    {
        var result = SeekBarChapterBuilder.Build(true, [], [], "Intro", "Outro");

        result.Should().BeEmpty();
    }
}
