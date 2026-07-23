namespace K7.Server.Application.Features.Users.Commands.ToggleUserActive;

public class ToggleUserActiveCommandValidator : AbstractValidator<ToggleUserActiveCommand>
{
    public ToggleUserActiveCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
