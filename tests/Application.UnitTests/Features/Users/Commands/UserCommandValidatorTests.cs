using K7.Server.Application.Features.Users.Commands.ChangePassword;
using K7.Server.Application.Features.Users.Commands.CreateUser;
using K7.Server.Application.Features.Users.Commands.SetPassword;
using K7.Server.Application.Features.Users.Commands.UpdateUserRole;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.UnitTests.Features.Users.Commands;

[TestFixture]
public class UserCommandValidatorTests
{
    [Test]
    public void CreateUser_ShouldFail_WhenUsernameEmptyOrRoleInvalid()
    {
        var validator = new CreateUserCommandValidator();

        var emptyUsername = validator.Validate(new CreateUserCommand
        {
            Username = "",
            Role = Roles.User
        });
        var invalidRole = validator.Validate(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.Guest
        });
        var valid = validator.Validate(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.Administrator
        });

        emptyUsername.IsValid.Should().BeFalse();
        invalidRole.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Test]
    public void ChangePassword_ShouldRequireCurrentAndMinLengthNewPassword()
    {
        var validator = new ChangePasswordCommandValidator();

        var tooShort = validator.Validate(new ChangePasswordCommand
        {
            CurrentPassword = "old",
            NewPassword = "12345"
        });
        var valid = validator.Validate(new ChangePasswordCommand
        {
            CurrentPassword = "old",
            NewPassword = "123456"
        });

        tooShort.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Test]
    public void SetPassword_ShouldRequireMinLength()
    {
        var validator = new SetPasswordCommandValidator();

        validator.Validate(new SetPasswordCommand { NewPassword = "12345" }).IsValid.Should().BeFalse();
        validator.Validate(new SetPasswordCommand { NewPassword = "123456" }).IsValid.Should().BeTrue();
    }

    [Test]
    public void UpdateUserRole_ShouldRejectGuestAndUnknownRoles()
    {
        var validator = new UpdateUserRoleCommandValidator();

        var id = Guid.NewGuid();
        validator.Validate(new UpdateUserRoleCommand { Id = id, Role = Roles.Guest }).IsValid.Should().BeFalse();
        validator.Validate(new UpdateUserRoleCommand { Id = id, Role = "Moderator" }).IsValid.Should().BeFalse();
        validator.Validate(new UpdateUserRoleCommand { Id = id, Role = Roles.User }).IsValid.Should().BeTrue();
    }
}
