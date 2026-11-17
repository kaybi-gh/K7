using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class PgsParserTests
{
    [Test]
    public void Feed_ShouldDecodeObjectAtItsPlacement_WhenDisplaySetIsComplete()
    {
        var stream = new PgsStreamBuilder().Show(12.5, x: 100, y: 900).ToArray();

        var frames = new PgsParser().Feed(stream);

        frames.Should().ContainSingle();
        var frame = frames[0];
        frame.TimeSeconds.Should().Be(12.5);
        frame.PlaneWidth.Should().Be(1920);
        frame.PlaneHeight.Should().Be(1080);
        frame.IsClear.Should().BeFalse();
        var pgsObject = frame.Objects.Should().ContainSingle().Subject;
        pgsObject.X.Should().Be(100);
        pgsObject.Y.Should().Be(900);
        pgsObject.Width.Should().Be(2);
        pgsObject.Height.Should().Be(2);
        // Line 1: two opaque white pixels. Line 2: a run of two half-transparent black ones.
        pgsObject.Rgba.Should().Equal(
            255, 255, 255, 255,
            255, 255, 255, 255,
            0, 0, 0, 128,
            0, 0, 0, 128);
    }

    [Test]
    public void Feed_ShouldReturnClearFrame_WhenCompositionPlacesNothing()
    {
        var stream = new PgsStreamBuilder().Show(1).Clear(3).ToArray();

        var frames = new PgsParser().Feed(stream);

        frames.Should().HaveCount(2);
        frames[1].TimeSeconds.Should().Be(3);
        frames[1].IsClear.Should().BeTrue();
    }

    [Test]
    public void Feed_ShouldParseTheSameFrames_WhenBytesArriveOneAtATime()
    {
        var stream = new PgsStreamBuilder().Show(1).Clear(3).Show(5).ToArray();
        var parser = new PgsParser();

        var frames = new List<PgsFrame>();
        foreach (var value in stream)
            frames.AddRange(parser.Feed([value]));

        frames.Select(f => f.TimeSeconds).Should().Equal(1, 3, 5);
        frames[0].Objects.Should().ContainSingle();
    }

    [Test]
    public void Feed_ShouldDropDisplaySet_WhenItsObjectWasNeverSeen()
    {
        // A window opened mid-epoch: the composition places an object defined earlier.
        var stream = new PgsStreamBuilder()
            .Composition(8, 1920, 1080, PgsStreamBuilder.NormalCase, 0, (1, 10, 10))
            .Palette(8, 0, (1, 235, 128, 128, 255))
            .End(8)
            .ToArray();

        new PgsParser().Feed(stream).Should().BeEmpty();
    }

    [Test]
    public void Feed_ShouldPlaceEarlierObjectAgain_WhenLaterDisplaySetStaysInTheEpoch()
    {
        var stream = new PgsStreamBuilder()
            .Show(1, x: 100, y: 900)
            .Composition(2, 1920, 1080, PgsStreamBuilder.NormalCase, 0, (1, 300, 200))
            .End(2)
            .ToArray();

        var frames = new PgsParser().Feed(stream);

        frames.Should().HaveCount(2);
        frames[1].Objects.Should().ContainSingle();
        frames[1].Objects[0].X.Should().Be(300);
        frames[1].Objects[0].Y.Should().Be(200);
    }

    [Test]
    public void Feed_ShouldForgetObjects_WhenANewEpochStarts()
    {
        var stream = new PgsStreamBuilder()
            .Show(1)
            .Composition(2, 1920, 1080, PgsStreamBuilder.EpochStart, 0, (1, 10, 10))
            .Palette(2, 0, (1, 235, 128, 128, 255))
            .End(2)
            .ToArray();

        var frames = new PgsParser().Feed(stream);

        frames.Should().ContainSingle();
        frames[0].TimeSeconds.Should().Be(1);
    }

    [Test]
    public void Feed_ShouldJoinFragments_WhenObjectSpansSeveralSegments()
    {
        var stream = new PgsStreamBuilder()
            .Composition(1, 1920, 1080, PgsStreamBuilder.EpochStart, 0, (1, 0, 0))
            .Palette(1, 0, (1, 235, 128, 128, 255), (2, 16, 128, 128, 128))
            .ObjectFragments(1, 1, 2, 2, PgsStreamBuilder.TwoByTwoRle, firstFragmentLength: 4)
            .End(1)
            .ToArray();

        var frames = new PgsParser().Feed(stream);

        frames.Should().ContainSingle();
        frames[0].Objects[0].Rgba.Should().HaveCount(16);
        frames[0].Objects[0].Rgba[12..].Should().Equal(0, 0, 0, 128);
    }

    [Test]
    public void Feed_ShouldKeepOnlyTheCropRectangle_WhenPlacementIsCropped()
    {
        var stream = new PgsStreamBuilder()
            .CroppedComposition(1, 1920, 1080, 0, objectId: 1, x: 50, y: 60, cropX: 0, cropY: 1, cropWidth: 2, cropHeight: 1)
            .Palette(1, 0, (1, 235, 128, 128, 255), (2, 16, 128, 128, 128))
            .Object(1, 1, 2, 2, PgsStreamBuilder.TwoByTwoRle)
            .End(1)
            .ToArray();

        var pgsObject = new PgsParser().Feed(stream)[0].Objects[0];

        pgsObject.Width.Should().Be(2);
        pgsObject.Height.Should().Be(1);
        pgsObject.Rgba.Should().Equal(0, 0, 0, 128, 0, 0, 0, 128);
    }

    [Test]
    public void Feed_ShouldUseBt601_WhenPlaneIsStandardDefinition()
    {
        // Pure red in BT.601 limited range (Y 81, Cb 90, Cr 240) is not red in BT.709.
        var stream = new PgsStreamBuilder()
            .Composition(1, 720, 576, PgsStreamBuilder.EpochStart, 0, (1, 0, 0))
            .Palette(1, 0, (1, 81, 240, 90, 255))
            .Object(1, 1, 1, 1, [1, 0, 0])
            .End(1)
            .ToArray();

        var rgba = new PgsParser().Feed(stream)[0].Objects[0].Rgba;

        rgba[0].Should().BeGreaterThanOrEqualTo(253);
        rgba[1].Should().BeLessThanOrEqualTo(2);
        rgba[2].Should().BeLessThanOrEqualTo(2);
    }

    [Test]
    public void Feed_ShouldResync_WhenStreamStartsInTheMiddleOfASegment()
    {
        byte[] garbage = [0x12, 0x34, 0x56];
        var stream = garbage.Concat(new PgsStreamBuilder().Show(4).ToArray()).ToArray();

        var frames = new PgsParser().Feed(stream);

        frames.Should().ContainSingle();
        frames[0].TimeSeconds.Should().Be(4);
    }
}
