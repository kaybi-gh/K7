namespace K7.Server.Application.Features.Users.Commands.UpdateSelfLibraryExclusions;

public class UpdateSelfLibraryExclusionsCommandValidator : AbstractValidator<UpdateSelfLibraryExclusionsCommand>
{
    public UpdateSelfLibraryExclusionsCommandValidator()
    {
        RuleFor(x => x.ExcludedLibraryIds).NotNull();
        RuleForEach(x => x.ExcludedLibraryIds).NotEmpty();
    }
}
