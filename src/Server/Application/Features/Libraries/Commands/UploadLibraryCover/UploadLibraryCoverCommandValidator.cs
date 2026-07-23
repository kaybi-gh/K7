namespace K7.Server.Application.Features.Libraries.Commands.UploadLibraryCover;

public class UploadLibraryCoverCommandValidator : AbstractValidator<UploadLibraryCoverCommand>
{
    public UploadLibraryCoverCommandValidator()
    {
        RuleFor(x => x.LibraryGroupId).NotEmpty();
        RuleFor(x => x.FileName).MaximumLength(500);
    }
}
