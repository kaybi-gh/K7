using K7.Shared;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Server.Application.UnitTests.Common;

public class AudioTrackDisplayHelperTests
{
    [Test]
    public void ResolveStoredName_ShouldKeepVariant_WhenLanguageTagIsVff()
    {
        AudioTrackDisplayHelper.ResolveStoredName(null, "vff").Should().Be("VFF");
        AudioTrackDisplayHelper.ResolveStoredName("French", "vfq").Should().Be("VFQ");
        AudioTrackDisplayHelper.ResolveStoredName("Commentary", "vff").Should().Be("Commentary (VFF)");
    }

    [Test]
    public void ResolveStoredName_ShouldKeepTitle_WhenAlreadyDistinctive()
    {
        AudioTrackDisplayHelper.ResolveStoredName("French VFF", "fra").Should().Be("French VFF");
        AudioTrackDisplayHelper.ResolveStoredName("VFQ", "fra").Should().Be("VFQ");
    }

    [Test]
    public void ResolveStoredName_ShouldFallBackToRawLanguage_WhenNoTitleOrVariant()
    {
        AudioTrackDisplayHelper.ResolveStoredName(null, "fra").Should().Be("fra");
    }

    [Test]
    public void GetDistinctiveName_ShouldKeepOriginalTitle_WhenNotJustTheLanguage()
    {
        AudioTrackDisplayHelper.GetDistinctiveName("vff", "fr").Should().Be("VFF");
        AudioTrackDisplayHelper.GetDistinctiveName("vfq", "fr").Should().Be("VFQ");
        AudioTrackDisplayHelper.GetDistinctiveName("France", "fr").Should().Be("France");
        AudioTrackDisplayHelper.GetDistinctiveName("Canadien", "fr").Should().Be("Canadien");
        AudioTrackDisplayHelper.GetDistinctiveName("fra", "fr").Should().BeNull();
        AudioTrackDisplayHelper.GetDistinctiveName("french", "fr").Should().BeNull();
    }

    [Test]
    public void FormatLabel_ShouldPutOriginalNameInParentheses_AfterNormalizedLanguage()
    {
        var vffLabel = AudioTrackDisplayHelper.FormatLabel(Track(1, "fr", "vff"));
        var franceLabel = AudioTrackDisplayHelper.FormatLabel(Track(2, "fr", "France"));
        var canadianLabel = AudioTrackDisplayHelper.FormatLabel(Track(3, "fr", "Canadien"));
        var genericLabel = AudioTrackDisplayHelper.FormatLabel(Track(4, "fr", "fra"));

        vffLabel.Should().Contain("(VFF)");
        franceLabel.Should().Contain("(France)");
        canadianLabel.Should().Contain("(Canadien)");
        genericLabel.Should().NotContain("(fra)");
        vffLabel.Should().NotBe(franceLabel);
        franceLabel.Should().NotBe(canadianLabel);
    }

    [Test]
    public void FormatSubtitleLabel_ShouldPutOriginalNameInParentheses_AfterNormalizedLanguage()
    {
        var vff = AudioTrackDisplayHelper.FormatSubtitleLabel(Sub(1, "fr", "vff"), "Full");
        var france = AudioTrackDisplayHelper.FormatSubtitleLabel(Sub(2, "fr", "France"), "Full");
        var canadian = AudioTrackDisplayHelper.FormatSubtitleLabel(Sub(3, "fr", "Canadien"), "Forced");
        var generic = AudioTrackDisplayHelper.FormatSubtitleLabel(Sub(4, "fr", "fra"), "Full");

        vff.Should().Contain("(VFF)");
        france.Should().Contain("(France)");
        canadian.Should().Contain("(Canadien)");
        generic.Should().NotContain("(fra)");
        generic.Should().Contain("Full");
        vff.Should().NotBe(france);
        france.Should().NotBe(canadian);
    }

    [Test]
    public void FormatHlsName_ShouldKeepVariantsUnique()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AudioTrackDisplayHelper.FormatHlsName("vff", "fr", 1, used).Should().Be("VFF");
        AudioTrackDisplayHelper.FormatHlsName("vfq", "fr", 2, used).Should().Be("VFQ");
        used.Should().BeEquivalentTo("VFF", "VFQ");
    }

    [Test]
    public void FormatHlsName_ShouldDisambiguateDuplicateNames()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AudioTrackDisplayHelper.FormatHlsName("fra", "fr", 1, used).Should().Be("fr 1");
        AudioTrackDisplayHelper.FormatHlsName("fra", "fr", 2, used).Should().Be("fr 2");
    }

    private static SubtitleFileTrackDto Sub(int index, string language, string name) => new()
    {
        Index = index,
        Language = language,
        Name = name,
        Codec = "subrip"
    };

    private static AudioFileTrackDto Track(int index, string language, string name) => new()
    {
        Index = index,
        Language = language,
        Name = name,
        Codec = "ac3",
        Channels = 6,
        ChannelLayout = "5.1"
    };
}
