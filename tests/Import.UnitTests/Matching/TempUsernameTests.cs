using K7.Import.Matching;

namespace K7.Import.UnitTests.Matching;

[TestFixture]
public class TempUsernameTests
{
    [Test]
    public void FromSource_ShouldReplaceSpacesWithDashes()
    {
        TempUsername.FromSource("plex", "John Doe").Should().Be("plex-john-doe");
    }

    [Test]
    public void FromSource_ShouldStripDisallowedAscii_WhenNameHasCaret()
    {
        TempUsername.FromSource("plex", "Neo^2").Should().Be("plex-neo-2");
    }

    [Test]
    public void FromSource_ShouldStripDiacritics()
    {
        TempUsername.FromSource("plex", "Renée").Should().Be("plex-renee");
    }

    [Test]
    public void FromSource_ShouldKeepAllowedSpecialChars()
    {
        TempUsername.FromSource("jellyfin", "user.name_1@test+x")
            .Should().Be("jellyfin-user.name_1@test+x");
    }

    [Test]
    public void FromSource_ShouldCollapseRepeatedSeparators()
    {
        TempUsername.FromSource("plex", "Neo^^^2").Should().Be("plex-neo-2");
    }

    [Test]
    public void FromSource_ShouldFallbackToUser_WhenOnlyDisallowedCharsRemain()
    {
        TempUsername.FromSource("plex", "^^^").Should().Be("plex-user");
    }

    [Test]
    public void Sanitize_ShouldReturnEmpty_WhenInputIsWhitespace()
    {
        TempUsername.Sanitize("   ").Should().BeEmpty();
    }
}
