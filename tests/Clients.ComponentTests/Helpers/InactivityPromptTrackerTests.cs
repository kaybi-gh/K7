using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class InactivityPromptTrackerTests
{
    private InactivityPromptTracker _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new InactivityPromptTracker();

    [Test]
    public void ShouldPrompt_ShouldReturnFalse_WhenEpisodeWasStartedByUser()
    {
        _sut.ShouldPrompt(1, Guid.NewGuid()).Should().BeFalse();
    }

    [Test]
    public void ShouldPrompt_ShouldReturnFalse_WhenBelowThreshold()
    {
        var episode = AutoAdvance(2);

        _sut.ShouldPrompt(3, episode).Should().BeFalse();
    }

    [Test]
    public void ShouldPrompt_ShouldReturnTrue_WhenThresholdReached()
    {
        var episode = AutoAdvance(3);

        _sut.ShouldPrompt(3, episode).Should().BeTrue();
    }

    [Test]
    public void ShouldPrompt_ShouldReturnFalse_WhenDisabled()
    {
        var episode = AutoAdvance(5);

        _sut.ShouldPrompt(0, episode).Should().BeFalse();
    }

    [Test]
    public void ShouldPrompt_ShouldReturnFalse_WhenUserWasActiveDuringEpisode()
    {
        var episode = AutoAdvance(3);

        _sut.RegisterUserActivity();

        _sut.ShouldPrompt(3, episode).Should().BeFalse();
    }

    [Test]
    public void ShouldPrompt_ShouldRestartCount_WhenEndedEpisodeWasNotAutoAdvanced()
    {
        AutoAdvance(3);
        _sut.ShouldPrompt(3, Guid.NewGuid()).Should().BeFalse();

        var episode = AutoAdvance(1);

        _sut.ShouldPrompt(3, episode).Should().BeFalse();
    }

    [Test]
    public void ShouldPrompt_ShouldKeepPrompting_WhenAskedAgainForSameEpisode()
    {
        var episode = AutoAdvance(2);

        _sut.ShouldPrompt(2, episode).Should().BeTrue();
        _sut.ShouldPrompt(2, episode).Should().BeTrue();
    }

    private Guid AutoAdvance(int times)
    {
        var episode = Guid.Empty;
        for (var i = 0; i < times; i++)
        {
            episode = Guid.NewGuid();
            _sut.RegisterAutoAdvance(episode);
        }

        return episode;
    }
}
