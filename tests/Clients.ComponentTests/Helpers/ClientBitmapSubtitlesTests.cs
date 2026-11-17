using K7.Clients.Shared.Helpers;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.QueryBuilders;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class ClientBitmapSubtitlesTests
{
    private const string HlsMime = "application/vnd.apple.mpegurl";
    private const string HlsUrl = "/api/indexed-files/1/hls-stream/manifest.m3u8?StreamSessionId=2";

    [TestCase("hdmv_pgs_subtitle", true)]
    [TestCase("PGSSUB", true)]
    [TestCase("pgs", true)]
    [TestCase("dvd_subtitle", false)]
    [TestCase("dvb_subtitle", false)]
    [TestCase(null, false)]
    public void IsPgs_ShouldMatchOnlyBluRayBitmapCodecs(string? codec, bool expected)
    {
        ClientBitmapSubtitles.IsPgs(Bitmap(codec)).Should().Be(expected);
    }

    [Test]
    public void IsPgs_ShouldBeFalse_WhenTrackIsTextOrMissing()
    {
        ClientBitmapSubtitles.IsPgs(null).Should().BeFalse();
        ClientBitmapSubtitles.IsPgs(new SubtitleFileTrackDto { Index = 2, Codec = "subrip", IsTextBased = true })
            .Should().BeFalse();
    }

    [Test]
    public void ShouldDraw_ShouldBeTrue_WhenPgsPlaysOnHlsWithoutBurnIn()
    {
        ClientBitmapSubtitles.ShouldDraw(Bitmap("hdmv_pgs_subtitle"), HlsMime, HlsUrl).Should().BeTrue();
    }

    [Test]
    public void ShouldDraw_ShouldBeFalse_WhenTheServerBurnsTheTrackIn()
    {
        ClientBitmapSubtitles.ShouldDraw(
                Bitmap("hdmv_pgs_subtitle"),
                HlsMime,
                HlsUrl + "&SubtitleBurnInStreamIndex=4")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldDraw_ShouldBeFalse_WhenTheFilePlaysDirect()
    {
        // The native player renders the embedded track itself.
        ClientBitmapSubtitles.ShouldDraw(
                Bitmap("hdmv_pgs_subtitle"),
                "video/x-matroska",
                "/api/indexed-files/1/direct-stream")
            .Should().BeFalse();
    }

    [Test]
    public void SupUriBuilder_ShouldWriteTheWindowWithInvariantNumbers()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        GetIndexedFileSubtitleSupQueryUriBuilder.Build(id, 4, 3590.5, 120)
            .Should().Be("/api/indexed-files/11111111-2222-3333-4444-555555555555/subtitles/4.sup?position=3590.5&duration=120");
    }

    private static SubtitleFileTrackDto Bitmap(string? codec) =>
        new() { Index = 4, Codec = codec, IsTextBased = false };
}
