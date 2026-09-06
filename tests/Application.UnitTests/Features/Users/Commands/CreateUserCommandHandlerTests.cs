using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.Users.Commands.CreateUser;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Features.Users.Commands;

[TestFixture]
public class CreateUserCommandHandlerTests
{
    [Test]
    public async Task Handle_ShouldThrowAppValidationException_WhenIdentityRejectsPassword()
    {
        var context = Substitute.For<IApplicationDbContext>();
        var identity = Substitute.For<IIdentityService>();
        var passwordPolicy = new StubPasswordPolicyService();

        identity.CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns((Result.Failure(["Passwords must be at least 10 characters."]), string.Empty));

        var handler = new CreateUserCommandHandler(context, identity, passwordPolicy);

        var act = () => handler.Handle(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.User,
            Password = "12345"
        }, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("Password");
    }

    [Test]
    public async Task Handle_ShouldGeneratePassword_WhenPasswordOmitted()
    {
        var context = Substitute.For<IApplicationDbContext>();
        var identity = Substitute.For<IIdentityService>();
        var passwordPolicy = new StubPasswordPolicyService();

        identity.CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns((Result.Failure(["Username 'kay' is already taken."]), string.Empty));

        var handler = new CreateUserCommandHandler(context, identity, passwordPolicy);

        var act = () => handler.Handle(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.User
        }, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("Username");

        await identity.Received(1).CreateUserAsync(
            "kay",
            Arg.Is<string>(password => password.Length >= PasswordPolicyDto.DefaultRequiredLength),
            null);
    }
}
