namespace K7.Server.Application.Features.Users.Commands.UpdateUserMediaExclusions;

public class UpdateUserMediaExclusionsCommandValidator : AbstractValidator<UpdateUserMediaExclusionsCommand>
{
    public UpdateUserMediaExclusionsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ExcludedMediaIds).NotNull();
        RuleForEach(x => x.ExcludedMediaIds).NotEmpty();
    }
}
