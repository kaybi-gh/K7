using K7.Import.Sources.Plex;

namespace K7.Import.UnitTests.Sources.Plex;

[TestFixture]
public class PlexHomeAuthTests
{
    [Test]
    public void TryParseSwitchResponse_ShouldReadXmlAuthTokenAndTitle()
    {
        var parsed = PlexHomeAuth.TryParseSwitchResponse(
            """
            <user id="20232382" title="Charlotte" username="" authenticationToken="home-tv-token" />
            """);

        parsed.Should().NotBeNull();
        parsed!.Value.Token.Should().Be("home-tv-token");
        parsed.Value.Id.Should().Be("20232382");
        parsed.Value.Title.Should().Be("Charlotte");
    }

    [Test]
    public void TryParseSwitchResponse_ShouldReadJsonAuthToken()
    {
        var parsed = PlexHomeAuth.TryParseSwitchResponse(
            """{"authToken":"json-token","id":"9","title":"Peter","username":"peter"}""");

        parsed.Should().NotBeNull();
        parsed!.Value.Token.Should().Be("json-token");
        parsed.Value.Title.Should().Be("Peter");
        parsed.Value.Username.Should().Be("peter");
    }

    [Test]
    public void IdentityMatches_ShouldAcceptRequestedIdOrTitle()
    {
        var identity = new PlexSwitchIdentity("t", "20232382", "Charlotte", null);

        PlexHomeAuth.IdentityMatches(identity, "20232382", "Charlotte", null).Should().BeTrue();
        PlexHomeAuth.IdentityMatches(identity, "20232382", "Other", null).Should().BeTrue();
        PlexHomeAuth.IdentityMatches(identity, "999", "charlotte", null).Should().BeTrue();
        PlexHomeAuth.IdentityMatches(identity, "999", "Kaybi", null).Should().BeFalse();
    }

    [Test]
    public void TryParseServerAccessToken_ShouldReadMatchingServerDevice()
    {
        var token = PlexHomeAuth.TryParseServerAccessToken(
            """
            <MediaContainer>
              <Device clientIdentifier="other" provides="client" accessToken="nope" />
              <Device clientIdentifier="MACHINE-1" provides="server" accessToken="charlotte-pms" />
            </MediaContainer>
            """,
            "MACHINE-1");

        token.Should().Be("charlotte-pms");
    }

    [Test]
    public void TryParseServerAccessToken_ShouldReadJsonResourcesArray()
    {
        var token = PlexHomeAuth.TryParseServerAccessToken(
            """
            [{"clientIdentifier":"MACHINE-1","provides":"server,player","accessToken":"from-json"}]
            """,
            "MACHINE-1");

        token.Should().Be("from-json");
    }

    [Test]
    public void TryParseServerAccessToken_ShouldIgnoreOtherServers()
    {
        var token = PlexHomeAuth.TryParseServerAccessToken(
            """
            <MediaContainer>
              <Device clientIdentifier="OTHER" provides="server" accessToken="wrong-server" />
            </MediaContainer>
            """,
            "MACHINE-1");

        token.Should().BeNull();
    }
}
