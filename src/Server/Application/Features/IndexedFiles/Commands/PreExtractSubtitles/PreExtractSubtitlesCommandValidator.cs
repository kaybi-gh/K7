namespace K7.Server.Application.Features.IndexedFiles.Commands.PreExtractSubtitles;

public class PreExtractSubtitlesCommandValidator : AbstractValidator<PreExtractSubtitlesCommand>
{
    public PreExtractSubtitlesCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
