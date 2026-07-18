namespace K7.Server.Application.Features.Users.Commands.ToggleMediaExclusion;

public class ToggleMediaExclusionCommandValidator : AbstractValidator<ToggleMediaExclusionCommand>
{
    public ToggleMediaExclusionCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
    }
}
