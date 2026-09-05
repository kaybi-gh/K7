using AwesomeAssertions;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using NUnit.Framework;

namespace K7.Clients.ComponentTests.Models;

public class VideoQualityOptionTests
{
    [Test]
    public void BuildOptionsForResolution_ShouldIncludeSourceHeightEncode_AfterOriginalRemux()
    {
        var options = VideoQualityOption.BuildOptionsForResolution(VideoResolutionIdentifier._1080p);

        options.Should().HaveCountGreaterThan(2);
        options[0].IsOriginal.Should().BeTrue();
        options[0].Label.Should().Be("Original (1080p)");
        options[0].Height.Should().Be(1080);

        options[1].IsOriginal.Should().BeFalse();
        options[1].Label.Should().Be("1080p");
        options[1].Height.Should().Be(1080);

        options[2].Label.Should().Be("720p");
        options[2].Height.Should().Be(720);
    }

    [Test]
    public void BuildOptionsForResolution_ShouldNotOfferHigherThanSource()
    {
        var options = VideoQualityOption.BuildOptionsForResolution(VideoResolutionIdentifier._720p);

        options.Should().Contain(q => q.IsOriginal && q.Label == "Original (720p)");
        options.Should().Contain(q => !q.IsOriginal && q.Label == "720p");
        options.Should().NotContain(q => q.Label == "1080p");
        options.Select(q => q.Height).Should().BeInDescendingOrder();
    }
}
