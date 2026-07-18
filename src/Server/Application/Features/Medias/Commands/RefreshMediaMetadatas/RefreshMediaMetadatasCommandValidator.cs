namespace K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;

public class RefreshMediaMetadatasCommandValidator : AbstractValidator<RefreshMediaMetadatasCommand>
{
    public RefreshMediaMetadatasCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.MetadataProviderExternalId).NotEmpty().MaximumLength(500);
        RuleFor(x => x.MetadataProviderName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Language).NotEmpty().MaximumLength(20);
        RuleFor(x => x.FallbackLanguage).NotEmpty().MaximumLength(20);
    }
}
