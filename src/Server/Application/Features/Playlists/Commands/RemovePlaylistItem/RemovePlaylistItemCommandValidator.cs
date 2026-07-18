namespace K7.Server.Application.Features.Playlists.Commands.RemovePlaylistItem;

public class RemovePlaylistItemCommandValidator : AbstractValidator<RemovePlaylistItemCommand>
{
    public RemovePlaylistItemCommandValidator()
    {
        RuleFor(x => x.PlaylistId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
    }
}
