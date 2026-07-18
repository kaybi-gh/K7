namespace K7.Server.Application.Features.SmartPlaylists.Commands.EvaluateSmartPlaylist;

public class EvaluateSmartPlaylistCommandValidator : AbstractValidator<EvaluateSmartPlaylistCommand>
{
    public EvaluateSmartPlaylistCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
