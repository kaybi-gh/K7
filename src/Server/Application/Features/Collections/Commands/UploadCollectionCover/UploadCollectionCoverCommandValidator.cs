namespace K7.Server.Application.Features.Collections.Commands.UploadCollectionCover;

public class UploadCollectionCoverCommandValidator : AbstractValidator<UploadCollectionCoverCommand>
{
    public UploadCollectionCoverCommandValidator()
    {
        RuleFor(x => x.CollectionId).NotEmpty();
        RuleFor(x => x.FileName).MaximumLength(500);
    }
}
