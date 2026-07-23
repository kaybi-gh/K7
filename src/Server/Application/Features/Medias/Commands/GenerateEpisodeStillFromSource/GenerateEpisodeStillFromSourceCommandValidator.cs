namespace K7.Server.Application.Features.Medias.Commands.GenerateEpisodeStillFromSource;

public class GenerateEpisodeStillFromSourceCommandValidator : AbstractValidator<GenerateEpisodeStillFromSourceCommand>
{
    public GenerateEpisodeStillFromSourceCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
    }
}
