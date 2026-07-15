using System.Text.RegularExpressions;
using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

public partial class StringParsingHelperTests
{
    [Test]
    public void TryApplyRegexes_ShouldReturnFalse_ForNullOrEmptyInput()
    {
        var regexes = new[] { YearRegex() };

        StringParsingHelper.TryApplyRegexes(null, regexes, recursive: false, out _).Should().BeFalse();
        StringParsingHelper.TryApplyRegexes("", regexes, recursive: false, out _).Should().BeFalse();
    }

    [Test]
    public void TryApplyRegexes_ShouldExtractFirstMatch_WhenNotRecursive()
    {
        var ok = StringParsingHelper.TryApplyRegexes(
            "Movie.Title.2020.x264",
            [YearRegex()],
            recursive: false,
            out var result);

        ok.Should().BeTrue();
        result!.Value.Output.Should().Be("2020");
        result.Value.TrimmedInput.Should().Be("Movie.Titlex264");
    }

    [Test]
    public void TryApplyRegexes_ShouldKeepFirstMatch_WhenRecursiveAndLaterPatternsMiss()
    {
        var ok = StringParsingHelper.TryApplyRegexes(
            "Movie.Title.2020.x264",
            [YearRegex(), SeasonEpisodeRegex()],
            recursive: true,
            out var result);

        ok.Should().BeTrue();
        result!.Value.Output.Should().Be("2020");
    }

    [GeneratedRegex(@"(?<noise>\.(?<output>\d{4})\.)", RegexOptions.IgnoreCase)]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"(?<noise>\.S(?<output>\d{2})E\d{2}\.)", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonEpisodeRegex();
}
