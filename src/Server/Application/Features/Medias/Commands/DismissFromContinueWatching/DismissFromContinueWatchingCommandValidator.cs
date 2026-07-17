namespace K7.Server.Application.Features.Medias.Commands.DismissFromContinueWatching;

public class DismissFromContinueWatchingCommandValidator : AbstractValidator<DismissFromContinueWatchingCommand>
{
    public DismissFromContinueWatchingCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
    }
}
