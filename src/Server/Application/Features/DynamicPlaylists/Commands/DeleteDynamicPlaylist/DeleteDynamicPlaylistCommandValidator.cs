namespace K7.Server.Application.Features.DynamicPlaylists.Commands.DeleteDynamicPlaylist;

public class DeleteDynamicPlaylistCommandValidator : AbstractValidator<DeleteDynamicPlaylistCommand>
{
    public DeleteDynamicPlaylistCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
