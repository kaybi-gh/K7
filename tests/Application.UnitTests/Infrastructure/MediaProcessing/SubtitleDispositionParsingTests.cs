using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

public class SubtitleDispositionParsingTests
{
    [Test]
    public void IsForcedSubtitle_ShouldUseDispositionFlag()
    {
        var disposition = new Dictionary<string, bool> { ["forced"] = true };

        MediaAnalysisService.IsForcedSubtitle(disposition, "French").Should().BeTrue();
    }

    [Test]
    public void IsForcedSubtitle_ShouldDetectTitleKeyword_WhenDispositionMissing()
    {
        MediaAnalysisService.IsForcedSubtitle(null, "French Forced").Should().BeTrue();
        MediaAnalysisService.IsForcedSubtitle(null, "Fra (Forcé)").Should().BeTrue();
    }

    [Test]
    public void IsForcedSubtitle_ShouldIgnoreNonForcedTitle()
    {
        MediaAnalysisService.IsForcedSubtitle(null, "French").Should().BeFalse();
        MediaAnalysisService.IsForcedSubtitle(null, "French non-forced").Should().BeFalse();
    }

    [Test]
    public void IsHearingImpairedSubtitle_ShouldDetectSdhInTitle()
    {
        MediaAnalysisService.IsHearingImpairedSubtitle(null, "English SDH").Should().BeTrue();
        MediaAnalysisService.IsHearingImpairedSubtitle(null, "English").Should().BeFalse();
    }
}
