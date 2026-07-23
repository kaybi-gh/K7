namespace K7.Server.Application.Features.Collections.Commands.AddCollectionItem;

public class AddCollectionItemCommandValidator : AbstractValidator<AddCollectionItemCommand>
{
    public AddCollectionItemCommandValidator()
    {
        RuleFor(x => x.CollectionId).NotEmpty();
        RuleFor(x => x.MediaId).NotEmpty();
    }
}
