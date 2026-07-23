namespace K7.Server.Application.Features.Playlists.Commands.RecordPlaylistPlayback;

public class RecordPlaylistPlaybackCommandValidator : AbstractValidator<RecordPlaylistPlaybackCommand>
{
    public RecordPlaylistPlaybackCommandValidator()
    {
        RuleFor(x => x.PlaylistId).NotEmpty();
    }
}
