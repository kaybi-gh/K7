namespace K7.Server.Application.Features.Users.Commands.MergeUsers;

public class MergeUsersCommandValidator : AbstractValidator<MergeUsersCommand>
{
    public MergeUsersCommandValidator()
    {
        RuleFor(x => x.SourceUserId)
            .NotEmpty();

        RuleFor(x => x.TargetUserId)
            .NotEmpty()
            .NotEqual(x => x.SourceUserId)
            .WithMessage("Source and target users must be different.");
    }
}
