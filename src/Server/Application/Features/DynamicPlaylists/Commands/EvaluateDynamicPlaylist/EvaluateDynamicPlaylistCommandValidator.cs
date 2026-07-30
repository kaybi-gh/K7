namespace K7.Server.Application.Features.DynamicPlaylists.Commands.EvaluateDynamicPlaylist;

public class EvaluateDynamicPlaylistCommandValidator : AbstractValidator<EvaluateDynamicPlaylistCommand>
{
    public EvaluateDynamicPlaylistCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
