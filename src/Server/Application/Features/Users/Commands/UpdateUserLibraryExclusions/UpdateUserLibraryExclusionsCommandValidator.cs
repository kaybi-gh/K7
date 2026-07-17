namespace K7.Server.Application.Features.Users.Commands.UpdateUserLibraryExclusions;

public class UpdateUserLibraryExclusionsCommandValidator : AbstractValidator<UpdateUserLibraryExclusionsCommand>
{
    public UpdateUserLibraryExclusionsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ExcludedLibraryIds).NotNull();
        RuleForEach(x => x.ExcludedLibraryIds).NotEmpty();
    }
}
