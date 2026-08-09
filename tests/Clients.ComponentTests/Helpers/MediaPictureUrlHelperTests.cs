using K7.Clients.Shared.Helpers;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MediaPictureUrlHelperTests
{
    [Test]
    public void ToDisplayUrl_ShouldPrefixServerBase_WhenRelativeMetadataPicturePath()
    {
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri("/api/metadata-pictures/abc?size=Small")
            .Returns(new Uri("https://k7.example/api/metadata-pictures/abc?size=Small"));

        var result = MediaPictureUrlHelper.ToDisplayUrl(apiClient, "/api/metadata-pictures/abc?size=Small");

        result.Should().Be("https://k7.example/api/metadata-pictures/abc?size=Small");
        apiClient.Received(1).GetAbsoluteUri("/api/metadata-pictures/abc?size=Small");
    }

    [Test]
    public void ToDisplayUrl_ShouldLeaveAbsoluteUrlUnchanged_WhenTmdbHttps()
    {
        var apiClient = Substitute.For<IK7ServerService>();
        const string tmdb = "https://image.tmdb.org/t/p/w500/poster.jpg";

        var result = MediaPictureUrlHelper.ToDisplayUrl(apiClient, tmdb);

        result.Should().Be(tmdb);
        apiClient.DidNotReceive().GetAbsoluteUri(Arg.Any<string?>());
    }

    [Test]
    public void ToDisplayUrl_ShouldLeaveStaticContentUnchanged_WhenBlazorAsset()
    {
        var apiClient = Substitute.For<IK7ServerService>();
        const string asset = "_content/K7.Clients.Shared.UI/images/person-placeholder.png";

        var result = MediaPictureUrlHelper.ToDisplayUrl(apiClient, asset);

        result.Should().Be(asset);
        apiClient.DidNotReceive().GetAbsoluteUri(Arg.Any<string?>());
    }

    [Test]
    public void ToDisplayUrl_ShouldLeaveOtherApiPathsUnchanged()
    {
        var apiClient = Substitute.For<IK7ServerService>();
        const string other = "/api/medias/abc/theme";

        var result = MediaPictureUrlHelper.ToDisplayUrl(apiClient, other);

        result.Should().Be(other);
        apiClient.DidNotReceive().GetAbsoluteUri(Arg.Any<string?>());
    }

    [Test]
    public void ToDisplayUrl_ShouldReturnNull_WhenUrlIsNull()
    {
        var apiClient = Substitute.For<IK7ServerService>();

        MediaPictureUrlHelper.ToDisplayUrl(apiClient, null).Should().BeNull();
        apiClient.DidNotReceive().GetAbsoluteUri(Arg.Any<string?>());
    }
}
