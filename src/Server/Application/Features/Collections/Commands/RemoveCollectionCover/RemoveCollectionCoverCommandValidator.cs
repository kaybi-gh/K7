namespace K7.Server.Application.Features.Collections.Commands.RemoveCollectionCover;

public class RemoveCollectionCoverCommandValidator : AbstractValidator<RemoveCollectionCoverCommand>
{
    public RemoveCollectionCoverCommandValidator()
    {
        RuleFor(x => x.CollectionId).NotEmpty();
    }
}
