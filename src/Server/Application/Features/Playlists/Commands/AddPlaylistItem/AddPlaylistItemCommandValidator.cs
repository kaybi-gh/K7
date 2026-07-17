namespace K7.Server.Application.Features.Playlists.Commands.AddPlaylistItem;

public class AddPlaylistItemCommandValidator : AbstractValidator<AddPlaylistItemCommand>
{
    public AddPlaylistItemCommandValidator()
    {
        RuleFor(x => x.PlaylistId).NotEmpty();
        RuleFor(x => x.MediaId).NotEmpty();
    }
}
