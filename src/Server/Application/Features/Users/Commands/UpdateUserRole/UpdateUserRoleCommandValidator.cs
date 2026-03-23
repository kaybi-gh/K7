using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Users.Commands.UpdateUserRole;

public class UpdateUserRoleCommandValidator : AbstractValidator<UpdateUserRoleCommand>
{
    private static readonly HashSet<string> ValidRoles = [Roles.Administrator, Roles.User, Roles.Guest];

    public UpdateUserRoleCommandValidator()
    {
        RuleFor(v => v.Role)
            .NotEmpty()
            .Must(r => ValidRoles.Contains(r))
                .WithMessage("Invalid role '{PropertyValue}'.")
            .Must(r => r != Roles.Guest)
                .WithMessage("Cannot assign the Guest role manually.");
    }
}
