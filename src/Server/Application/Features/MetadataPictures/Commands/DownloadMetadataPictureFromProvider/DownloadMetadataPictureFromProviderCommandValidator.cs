namespace K7.Server.Application.Features.MetadataPictures.Commands.DownloadMetadataPictureFromProvider;

public class DownloadMetadataPictureFromProviderCommandValidator : AbstractValidator<DownloadMetadataPictureFromProviderCommand>
{
    public DownloadMetadataPictureFromProviderCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
