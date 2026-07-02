using FluentAssertions;
using K7.Server.Application.Features.SharedProfiles.Commands.CreateSharedProfile;
using NUnit.Framework;

namespace K7.Server.Application.UnitTests.Features.SharedProfiles;

public class CreateSharedProfileCommandValidatorTests
{
    private readonly CreateSharedProfileCommandValidator _validator = new();

    [Test]
    public void Validate_ShouldFail_WhenLessThanTwoMembers()
    {
        var command = new CreateSharedProfileCommand
        {
            Name = "Test",
            HostUserId = Guid.NewGuid(),
            MemberUserIds = [Guid.NewGuid()]
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldFail_WhenHostNotInMembers()
    {
        var hostId = Guid.NewGuid();
        var command = new CreateSharedProfileCommand
        {
            Name = "Test",
            HostUserId = hostId,
            MemberUserIds = [Guid.NewGuid(), Guid.NewGuid()]
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_ShouldPass_WhenHostIsMemberAndAtLeastTwoMembers()
    {
        var hostId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var command = new CreateSharedProfileCommand
        {
            Name = "Kay & Marie",
            HostUserId = hostId,
            MemberUserIds = [hostId, memberId]
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
