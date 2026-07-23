namespace K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;

public class GenerateMetadataPictureVariantsCommandValidator : AbstractValidator<GenerateMetadataPictureVariantsCommand>
{
    public GenerateMetadataPictureVariantsCommandValidator()
    {
        RuleFor(x => x.MetadataPictureId).NotEmpty();
    }
}
