namespace K7.Server.Application.Features.Collections.Commands.DeleteCollection;

public class DeleteCollectionCommandValidator : AbstractValidator<DeleteCollectionCommand>
{
    public DeleteCollectionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
