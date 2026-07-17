namespace K7.Server.Application.Features.Playlists.Commands.RemovePlaylistCover;

public class RemovePlaylistCoverCommandValidator : AbstractValidator<RemovePlaylistCoverCommand>
{
    public RemovePlaylistCoverCommandValidator()
    {
        RuleFor(x => x.PlaylistId).NotEmpty();
    }
}
