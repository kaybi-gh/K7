using K7.Server.Application.Features.Users.Commands.ChangePassword;
using K7.Server.Application.Features.Users.Commands.CreateUser;
using K7.Server.Application.Features.Users.Commands.ResetUserPassword;
using K7.Server.Application.Features.Users.Commands.SetPassword;
using K7.Server.Application.Features.Users.Commands.UpdateUserRole;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Features.Users.Commands;

[TestFixture]
public class UserCommandValidatorTests
{
    private static readonly StubPasswordPolicyService PasswordPolicy = new();

    [Test]
    public void CreateUser_ShouldFail_WhenUsernameEmptyOrRoleInvalid()
    {
        var validator = new CreateUserCommandValidator(PasswordPolicy);

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
    public void CreateUser_ShouldAllowMissingEmail_AndRejectInvalidEmail()
    {
        var validator = new CreateUserCommandValidator(PasswordPolicy);

        validator.Validate(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.User,
            Email = null
        }).IsValid.Should().BeTrue();

        validator.Validate(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.User,
            Email = ""
        }).IsValid.Should().BeTrue();

        validator.Validate(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.User,
            Email = "kay@example.com"
        }).IsValid.Should().BeTrue();

        validator.Validate(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.User,
            Email = "not-an-email"
        }).IsValid.Should().BeFalse();
    }

    [Test]
    public async Task CreateUser_ShouldRejectWeakPassword_WhenPasswordProvided()
    {
        var validator = new CreateUserCommandValidator(PasswordPolicy);

        (await validator.ValidateAsync(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.User,
            Password = "12345"
        })).IsValid.Should().BeFalse();

        (await validator.ValidateAsync(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.User,
            Password = "Password12"
        })).IsValid.Should().BeTrue();
    }

    [Test]
    public async Task CreateUser_ShouldRejectPassword_WhenCustomPolicyRequiresSpecialCharacter()
    {
        var validator = new CreateUserCommandValidator(new StubPasswordPolicyService(new PasswordPolicyDto
        {
            RequireNonAlphanumeric = true
        }));

        (await validator.ValidateAsync(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.User,
            Password = "Password12"
        })).IsValid.Should().BeFalse();

        (await validator.ValidateAsync(new CreateUserCommand
        {
            Username = "kay",
            Role = Roles.User,
            Password = "Password12!"
        })).IsValid.Should().BeTrue();
    }

    [Test]
    public async Task ResetUserPassword_ShouldRequirePasswordPolicy()
    {
        var validator = new ResetUserPasswordCommandValidator(PasswordPolicy);
        var userId = Guid.NewGuid();

        (await validator.ValidateAsync(new ResetUserPasswordCommand
        {
            UserId = userId,
            NewPassword = "12345"
        })).IsValid.Should().BeFalse();

        (await validator.ValidateAsync(new ResetUserPasswordCommand
        {
            UserId = userId,
            NewPassword = "Password12"
        })).IsValid.Should().BeTrue();
    }

    [Test]
    public async Task ChangePassword_ShouldRequireCurrentAndPasswordPolicy()
    {
        var validator = new ChangePasswordCommandValidator(PasswordPolicy);

        var missingCurrent = await validator.ValidateAsync(new ChangePasswordCommand
        {
            CurrentPassword = "",
            NewPassword = "Password12"
        });
        var tooWeak = await validator.ValidateAsync(new ChangePasswordCommand
        {
            CurrentPassword = "old",
            NewPassword = "12345"
        });
        var valid = await validator.ValidateAsync(new ChangePasswordCommand
        {
            CurrentPassword = "old",
            NewPassword = "Password12"
        });

        missingCurrent.IsValid.Should().BeFalse();
        tooWeak.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Test]
    public async Task SetPassword_ShouldRequirePasswordPolicy()
    {
        var validator = new SetPasswordCommandValidator(PasswordPolicy);

        (await validator.ValidateAsync(new SetPasswordCommand { NewPassword = "12345" })).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new SetPasswordCommand { NewPassword = "Password12" })).IsValid.Should().BeTrue();
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
