using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.PasswordPolicySettings.Commands.UpdatePasswordPolicy;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Features.PasswordPolicySettings;

[TestFixture]
public class UpdatePasswordPolicyCommandHandlerTests
{
    [Test]
    public async Task Handle_ShouldNormalizeAndPersistPolicy()
    {
        var passwordPolicy = Substitute.For<IPasswordPolicyService>();
        var handler = new UpdatePasswordPolicyCommandHandler(passwordPolicy);

        var result = await handler.Handle(new UpdatePasswordPolicyCommand
        {
            Policy = new PasswordPolicyDto
            {
                RequiredLength = 400,
                RequiredUniqueChars = 200,
                RequireNonAlphanumeric = true
            }
        }, CancellationToken.None);

        result.RequiredLength.Should().Be(PasswordPolicyDto.MaxRequiredLength);
        result.RequiredUniqueChars.Should().Be(PasswordPolicyDto.MaxRequiredLength);
        result.RequireNonAlphanumeric.Should().BeTrue();

        await passwordPolicy.Received(1).UpdateAsync(result, Arg.Any<CancellationToken>());
    }
}
