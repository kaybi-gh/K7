using K7.Shared;

namespace K7.Server.Application.UnitTests.Common;

public class LanguageNormalizerTests
{
    [TestCase("fr", "fr")]
    [TestCase("FR", "fr")]
    [TestCase("Fra", "fr")]
    [TestCase("fre", "fr")]
    [TestCase("FRE", "fr")]
    [TestCase("french", "fr")]
    [TestCase("French", "fr")]
    [TestCase("FRENCH", "fr")]
    [TestCase("francais", "fr")]
    [TestCase("français", "fr")]
    [TestCase("vff", "fr")]
    [TestCase("VFF", "fr")]
    [TestCase("vfq", "fr")]
    [TestCase("VFQ", "fr")]
    [TestCase("vf", "fr")]
    [TestCase("vfi", "fr")]
    public void NormalizeToIso6391_French_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("en", "en")]
    [TestCase("EN", "en")]
    [TestCase("eng", "en")]
    [TestCase("ENG", "en")]
    [TestCase("english", "en")]
    [TestCase("English", "en")]
    [TestCase("anglais", "en")]
    public void NormalizeToIso6391_English_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("de", "de")]
    [TestCase("deu", "de")]
    [TestCase("ger", "de")]
    [TestCase("german", "de")]
    [TestCase("deutsch", "de")]
    [TestCase("allemand", "de")]
    public void NormalizeToIso6391_German_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("es", "es")]
    [TestCase("spa", "es")]
    [TestCase("spanish", "es")]
    [TestCase("espanol", "es")]
    [TestCase("espagnol", "es")]
    [TestCase("castellano", "es")]
    [TestCase("lat", "es")]
    public void NormalizeToIso6391_Spanish_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("it", "it")]
    [TestCase("ita", "it")]
    [TestCase("italian", "it")]
    [TestCase("italiano", "it")]
    [TestCase("italien", "it")]
    public void NormalizeToIso6391_Italian_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("ja", "ja")]
    [TestCase("jpn", "ja")]
    [TestCase("japanese", "ja")]
    [TestCase("japonais", "ja")]
    public void NormalizeToIso6391_Japanese_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("ko", "ko")]
    [TestCase("kor", "ko")]
    [TestCase("korean", "ko")]
    [TestCase("coreen", "ko")]
    public void NormalizeToIso6391_Korean_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("pt", "pt")]
    [TestCase("por", "pt")]
    [TestCase("portuguese", "pt")]
    [TestCase("portugais", "pt")]
    [TestCase("portugues", "pt")]
    public void NormalizeToIso6391_Portuguese_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("ru", "ru")]
    [TestCase("rus", "ru")]
    [TestCase("russian", "ru")]
    [TestCase("russe", "ru")]
    public void NormalizeToIso6391_Russian_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("zh", "zh")]
    [TestCase("zho", "zh")]
    [TestCase("chi", "zh")]
    [TestCase("chinese", "zh")]
    [TestCase("chinois", "zh")]
    [TestCase("cmn", "zh")]
    public void NormalizeToIso6391_Chinese_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("ar", "ar")]
    [TestCase("ara", "ar")]
    [TestCase("arabic", "ar")]
    [TestCase("arabe", "ar")]
    public void NormalizeToIso6391_Arabic_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("nl", "nl")]
    [TestCase("nld", "nl")]
    [TestCase("dut", "nl")]
    [TestCase("dutch", "nl")]
    [TestCase("neerlandais", "nl")]
    [TestCase("nederlands", "nl")]
    [TestCase("flemish", "nl")]
    public void NormalizeToIso6391_Dutch_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("pl", "pl")]
    [TestCase("pol", "pl")]
    [TestCase("polish", "pl")]
    [TestCase("polonais", "pl")]
    public void NormalizeToIso6391_Polish_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("no", "no")]
    [TestCase("nor", "no")]
    [TestCase("nob", "no")]
    [TestCase("nno", "no")]
    [TestCase("nb", "no")]
    [TestCase("nn", "no")]
    [TestCase("norwegian", "no")]
    [TestCase("norvegien", "no")]
    public void NormalizeToIso6391_Norwegian_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("cs", "cs")]
    [TestCase("ces", "cs")]
    [TestCase("cze", "cs")]
    [TestCase("czech", "cs")]
    [TestCase("tcheque", "cs")]
    public void NormalizeToIso6391_Czech_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("el", "el")]
    [TestCase("ell", "el")]
    [TestCase("gre", "el")]
    [TestCase("greek", "el")]
    [TestCase("grec", "el")]
    public void NormalizeToIso6391_Greek_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("fa", "fa")]
    [TestCase("fas", "fa")]
    [TestCase("per", "fa")]
    [TestCase("persian", "fa")]
    [TestCase("farsi", "fa")]
    [TestCase("persan", "fa")]
    public void NormalizeToIso6391_Persian_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [Test]
    public void NormalizeToIso6391_Null_ReturnsNull()
    {
        LanguageNormalizer.NormalizeToIso6391(null).Should().BeNull();
    }

    [Test]
    public void NormalizeToIso6391_Empty_ReturnsNull()
    {
        LanguageNormalizer.NormalizeToIso6391("").Should().BeNull();
    }

    [Test]
    public void NormalizeToIso6391_Whitespace_ReturnsNull()
    {
        LanguageNormalizer.NormalizeToIso6391("   ").Should().BeNull();
    }

    [Test]
    public void NormalizeToIso6391_UnknownLanguage_ReturnsNull()
    {
        LanguageNormalizer.NormalizeToIso6391("klingon").Should().BeNull();
    }

    [TestCase("und", "und")]
    [TestCase("undetermined", "und")]
    [TestCase("unknown", "und")]
    public void NormalizeToIso6391_Undetermined_Variants(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [Test]
    public void NormalizeOrPassthrough_Null_ReturnsUnd()
    {
        LanguageNormalizer.NormalizeOrPassthrough(null).Should().Be("und");
    }

    [Test]
    public void NormalizeOrPassthrough_Empty_ReturnsUnd()
    {
        LanguageNormalizer.NormalizeOrPassthrough("").Should().Be("und");
    }

    [Test]
    public void NormalizeOrPassthrough_UnknownInput_ReturnsLowercasedInput()
    {
        LanguageNormalizer.NormalizeOrPassthrough("Klingon").Should().Be("klingon");
    }

    [Test]
    public void NormalizeOrPassthrough_KnownInput_ReturnsNormalized()
    {
        LanguageNormalizer.NormalizeOrPassthrough("FRE").Should().Be("fr");
    }

    [TestCase(" fr ", "fr")]
    [TestCase(" ENG ", "en")]
    [TestCase("  VFF  ", "fr")]
    public void NormalizeToIso6391_TrimsWhitespace(string input, string expected)
    {
        LanguageNormalizer.NormalizeToIso6391(input).Should().Be(expected);
    }

    [TestCase("Français Complets (Colors)", "fr")]
    [TestCase("English Full Subtitles", "en")]
    [TestCase("English (SDH)", "en")]
    [TestCase("VFF Forced", "fr")]
    [TestCase("Forced - Français", "fr")]
    [TestCase("Complets en Français", "fr")]
    public void InferFromTrackTitle_ShouldInferLanguage_WhenTitleContainsHint(string title, string expected)
    {
        LanguageNormalizer.InferFromTrackTitle(title).Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("SDH")]
    [TestCase("Forced")]
    [TestCase("Complets (Colors)")]
    public void InferFromTrackTitle_ShouldReturnNull_WhenTitleHasNoLanguageHint(string? title)
    {
        LanguageNormalizer.InferFromTrackTitle(title).Should().BeNull();
    }

    [Test]
    public void ResolveSubtitleLanguage_ShouldInferFromTitle_WhenContainerLanguageMissing()
    {
        LanguageNormalizer.ResolveSubtitleLanguage(null, "Français Complets (Colors)").Should().Be("fr");
    }

    [Test]
    public void ResolveSubtitleLanguage_ShouldKeepContainerLanguage_WhenPresent()
    {
        LanguageNormalizer.ResolveSubtitleLanguage("eng", "Français Complets (Colors)").Should().Be("en");
    }

    [Test]
    public void ResolveSubtitleLanguage_ShouldInferFromTitle_WhenContainerLanguageUndetermined()
    {
        LanguageNormalizer.ResolveSubtitleLanguage("und", "English (SDH)").Should().Be("en");
    }
}
