using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MetadataLocalizedTextTests
{
    [Test]
    public void Prefer_ShouldKeepFrenchTitle_WhenAlreadyLatin()
    {
        MetadataLocalizedText.Prefer("Bleach", "ブリーチ", "fr").Should().Be("Bleach");
    }

    [Test]
    public void Prefer_ShouldUseEnglishFallback_WhenPrimaryIsJapanese()
    {
        MetadataLocalizedText.Prefer("死神になっちゃった日", "The Day I Became a Shinigami", "fr")
            .Should().Be("The Day I Became a Shinigami");
    }

    [Test]
    public void Prefer_ShouldKeepJapanese_WhenLibraryLanguageIsJapanese()
    {
        MetadataLocalizedText.Prefer("ブリーチ", "Bleach", "ja").Should().Be("ブリーチ");
    }

    [Test]
    public void ShouldFetchFallback_ShouldBeTrue_WhenTitleIsJapaneseAndLibraryIsFrench()
    {
        MetadataLocalizedText.ShouldFetchFallback("ブリーチ", "Un synopsis francais", "fr", "en")
            .Should().BeTrue();
    }

    [Test]
    public void ShouldFetchFallback_ShouldBeTrue_WhenOverviewIsJapanese()
    {
        MetadataLocalizedText.ShouldFetchFallback("Bleach", "死神になっちゃった日", "fr", "en")
            .Should().BeTrue();
    }

    [Test]
    public void ShouldFetchFallback_ShouldBeFalse_WhenTitleIsLatinAndOverviewEmpty()
    {
        MetadataLocalizedText.ShouldFetchFallback("Bleach", null, "fr", "en")
            .Should().BeFalse();
    }

    [Test]
    public void ShouldFetchFallback_ShouldBeFalse_WhenFallbackIsSameLanguage()
    {
        MetadataLocalizedText.ShouldFetchFallback("ブリーチ", null, "fr", "fr-FR")
            .Should().BeFalse();
    }

    [Test]
    public void LanguageKey_ShouldNormalizeThreeLetterJapanese()
    {
        MetadataLanguageScript.LanguageKey("jpn").Should().Be("ja");
        MetadataLanguageScript.UsesLatinMetadata("jpn").Should().BeFalse();
    }
}
