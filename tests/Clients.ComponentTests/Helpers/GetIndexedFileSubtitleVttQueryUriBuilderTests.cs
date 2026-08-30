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
    public void Route_ShouldMatchEndpointTemplate()
    {
        GetIndexedFileSubtitleVttQueryUriBuilder.Route
            .Should().Be("/api/indexed-files/{id}/subtitles/{subtitleTrackIndex}.vtt");
    }
}
