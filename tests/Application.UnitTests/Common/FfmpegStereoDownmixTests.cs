using K7.Server.Application.Common;

namespace K7.Server.Application.UnitTests.Common;

public class FfmpegStereoDownmixTests
{
    [Test]
    public void TryBuildPanFilter_ShouldUseBackChannels_For51()
    {
        var filter = FfmpegStereoDownmix.TryBuildPanFilter("5.1", 6, outputChannels: 2);

        filter.Should().Be("pan=stereo|FL=0.8*FC+0.6*FL+0.6*BL+0.5*LFE|FR=0.8*FC+0.6*FR+0.6*BR+0.5*LFE");
    }

    [Test]
    public void TryBuildPanFilter_ShouldUseSideChannels_For51Side()
    {
        // AC3 5.1 decodes to 5.1(side); referencing BL/BR there makes ffmpeg fail.
        var filter = FfmpegStereoDownmix.TryBuildPanFilter("5.1(side)", 6, outputChannels: 2);

        filter.Should().Contain("SL").And.Contain("SR").And.NotContain("BL");
    }

    [Test]
    public void TryBuildPanFilter_ShouldAcceptVerboseFfprobeLayout()
    {
        var filter = FfmpegStereoDownmix.TryBuildPanFilter("7.1 (FL+FR+FC+LFE+BL+BR+SL+SR)", 8, outputChannels: 2);

        filter.Should().StartWith("pan=stereo|").And.Contain("0.4*BL+0.4*SL");
    }

    [Test]
    public void TryBuildPanFilter_ShouldReturnNull_WhenNotADownmixOrLayoutUnknown()
    {
        FfmpegStereoDownmix.TryBuildPanFilter("5.1", 6, outputChannels: 6).Should().BeNull();
        FfmpegStereoDownmix.TryBuildPanFilter("stereo", 2, outputChannels: 2).Should().BeNull();
        FfmpegStereoDownmix.TryBuildPanFilter(null, 6, outputChannels: 2).Should().BeNull();
        FfmpegStereoDownmix.TryBuildPanFilter("hexagonal", 6, outputChannels: 2).Should().BeNull();
    }

    [Test]
    public void ToFilterArgument_ShouldQuoteThePipeSeparatedMatrix()
    {
        FfmpegStereoDownmix.ToFilterArgument("pan=stereo|FL=FC|FR=FC")
            .Should().Be("-af \"pan=stereo|FL=FC|FR=FC\"");
    }
}
