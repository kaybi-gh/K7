namespace K7.Server.Application.Features.Playlists.Commands.DeletePlaylist;

public class DeletePlaylistCommandValidator : AbstractValidator<DeletePlaylistCommand>
{
    public DeletePlaylistCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
