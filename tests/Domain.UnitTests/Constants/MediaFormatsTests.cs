using K7.Server.Domain.Entities.MediaFormats;
using DomainConstants = K7.Server.Domain.Constants.Constants;

namespace K7.Server.Domain.UnitTests.Constants;

[TestFixture]
public class MediaFormatCatalogTests
{
    [Test]
    public void Catalog_ShouldIncludeAudioFormat_ForEveryVideoContainerAudioCodec()
    {
        var audioKeys = DomainConstants.MediaFormats
            .OfType<AudioMediaFormat>()
            .Select(a => (a.Container, a.Codec))
            .ToHashSet();

        foreach (var video in DomainConstants.MediaFormats.OfType<VideoMediaFormat>())
        {
            if (string.IsNullOrEmpty(video.AudioCodec))
                continue;

            audioKeys.Should().Contain(
                (video.Container, video.AudioCodec),
                because: video.Id + " needs a matching AudioMediaFormat for Direct Play");
        }
    }

    [Test]
    public void Catalog_ShouldHaveUniqueIds()
    {
        var ids = DomainConstants.MediaFormats.Select(f => f.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void Catalog_ShouldIncludeAv1AndAv2_ForPrimaryContainers()
    {
        var videoKeys = DomainConstants.MediaFormats
            .OfType<VideoMediaFormat>()
            .Select(v => (v.Container, v.VideoCodec))
            .ToHashSet();

        foreach (var container in new[] { "matroska", "mp4", "webm" })
        {
            videoKeys.Should().Contain((container, "av1"));
            videoKeys.Should().Contain((container, "av2"));
        }

        videoKeys.Should().Contain(("mpegts", "av1"));
        videoKeys.Should().Contain(("mov", "av1"));
        videoKeys.Should().Contain(("m4v", "av1"));
    }
}
