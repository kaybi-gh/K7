using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

public class HlsAudioRemuxRecoveryTests
{
    [Test]
    public void IsRemuxAudioAppendFailure_ShouldReturnTrue_WhenMessageMatchesVhs()
    {
        HlsAudioRemuxRecovery.IsRemuxAudioAppendFailure(
                "audio append of 288925b failed for segment #8 in playlist 0")
            .Should().BeTrue();
    }

    [Test]
    public void IsRemuxAudioAppendFailure_ShouldReturnFalse_WhenMessageEmpty()
    {
        HlsAudioRemuxRecovery.IsRemuxAudioAppendFailure(null).Should().BeFalse();
        HlsAudioRemuxRecovery.IsRemuxAudioAppendFailure("network error").Should().BeFalse();
    }
}
