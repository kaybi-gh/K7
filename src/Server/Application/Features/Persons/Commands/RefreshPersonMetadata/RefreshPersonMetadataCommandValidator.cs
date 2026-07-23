namespace K7.Server.Application.Features.Persons.Commands.RefreshPersonMetadata;

public class RefreshPersonMetadataCommandValidator : AbstractValidator<RefreshPersonMetadataCommand>
{
    public RefreshPersonMetadataCommandValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.ProviderName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ProviderId).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Language).NotEmpty().MaximumLength(20);
    }
}
