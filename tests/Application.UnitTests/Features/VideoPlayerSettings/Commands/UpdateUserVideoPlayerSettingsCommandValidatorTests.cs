using K7.Server.Application.Features.VideoPlayerSettings.Commands.UpdateUserVideoPlayerSettings;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Features.VideoPlayerSettings.Commands;

[TestFixture]
public class UpdateUserVideoPlayerSettingsCommandValidatorTests
{
    private UpdateUserVideoPlayerSettingsCommandValidator _validator = null!;

    [SetUp]
    public void SetUp() => _validator = new UpdateUserVideoPlayerSettingsCommandValidator();

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(10)]
    public void Validate_ShouldPass_WhenInactivityPromptInRange(int episodes)
    {
        var command = new UpdateUserVideoPlayerSettingsCommand
        {
            Settings = new VideoPlayerSettingsDto { InactivityPromptAfterEpisodes = episodes }
        };

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [TestCase(-1)]
    [TestCase(11)]
    public void Validate_ShouldFail_WhenInactivityPromptOutOfRange(int episodes)
    {
        var command = new UpdateUserVideoPlayerSettingsCommand
        {
            Settings = new VideoPlayerSettingsDto { InactivityPromptAfterEpisodes = episodes }
        };

        _validator.Validate(command).IsValid.Should().BeFalse();
    }

    [TestCase(NextEpisodeBehavior.PlayAfterCountdown)]
    [TestCase(NextEpisodeBehavior.WaitForChoice)]
    [TestCase(NextEpisodeBehavior.ClosePlayer)]
    public void Validate_ShouldPass_WhenNextEpisodeBehaviorIsDefined(NextEpisodeBehavior behavior)
    {
        var command = new UpdateUserVideoPlayerSettingsCommand
        {
            Settings = new VideoPlayerSettingsDto { NextEpisodeBehavior = behavior }
        };

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_ShouldFail_WhenNextEpisodeBehaviorIsUndefined()
    {
        var command = new UpdateUserVideoPlayerSettingsCommand
        {
            Settings = new VideoPlayerSettingsDto { NextEpisodeBehavior = (NextEpisodeBehavior)42 }
        };

        _validator.Validate(command).IsValid.Should().BeFalse();
    }
}
