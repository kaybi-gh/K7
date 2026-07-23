namespace K7.Server.Application.Features.Persons.Commands.QueueRefreshPersonMetadata;

public class QueueRefreshPersonMetadataCommandValidator : AbstractValidator<QueueRefreshPersonMetadataCommand>
{
    public QueueRefreshPersonMetadataCommandValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty();
    }
}
