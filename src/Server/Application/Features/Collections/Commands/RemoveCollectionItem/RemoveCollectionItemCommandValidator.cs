namespace K7.Server.Application.Features.Collections.Commands.RemoveCollectionItem;

public class RemoveCollectionItemCommandValidator : AbstractValidator<RemoveCollectionItemCommand>
{
    public RemoveCollectionItemCommandValidator()
    {
        RuleFor(x => x.CollectionId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
    }
}
