namespace K7.Server.Application.Features.SmartPlaylists.Commands.DeleteSmartPlaylist;

public class DeleteSmartPlaylistCommandValidator : AbstractValidator<DeleteSmartPlaylistCommand>
{
    public DeleteSmartPlaylistCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
