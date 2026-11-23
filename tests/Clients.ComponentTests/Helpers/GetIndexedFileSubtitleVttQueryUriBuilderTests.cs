using K7.Shared.QueryBuilders;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class GetIndexedFileSubtitleVttQueryUriBuilderTests
{
    [Test]
    public void Build_ShouldEmbedFileIdAndTrackIndex()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var uri = GetIndexedFileSubtitleVttQueryUriBuilder.Build(id, 3);

        uri.Should().Be("/api/indexed-files/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/subtitles/3.vtt");
    }

    [Test]
    public void Build_ShouldAppendPosition_WithInvariantDecimalSeparator()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var culture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
        try
        {
            GetIndexedFileSubtitleVttQueryUriBuilder.Build(id, 3, 3725.46)
                .Should().Be("/api/indexed-files/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/subtitles/3.vtt?position=3725.5");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }
    }

    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void Build_ShouldOmitPosition_WhenNotAValidTime(double position)
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        GetIndexedFileSubtitleVttQueryUriBuilder.Build(id, 3, position)
            .Should().Be("/api/indexed-files/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/subtitles/3.vtt");
    }

    [Test]
    public void Route_ShouldMatchEndpointTemplate()
    {
        GetIndexedFileSubtitleVttQueryUriBuilder.Route
            .Should().Be("/api/indexed-files/{id}/subtitles/{subtitleTrackIndex}.vtt");
    }
}
