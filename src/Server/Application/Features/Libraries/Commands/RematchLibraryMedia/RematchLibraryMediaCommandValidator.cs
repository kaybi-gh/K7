namespace K7.Server.Application.Features.Libraries.Commands.RematchLibraryMedia;

public class RematchLibraryMediaCommandValidator : AbstractValidator<RematchLibraryMediaCommand>
{
    public RematchLibraryMediaCommandValidator()
    {
        RuleFor(x => x.LibraryId).NotEmpty();
    }
}
