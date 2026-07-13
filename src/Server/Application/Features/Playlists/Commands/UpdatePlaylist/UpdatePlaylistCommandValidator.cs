namespace K7.Server.Application.Features.Playlists.Commands.UpdatePlaylist;

public class UpdatePlaylistCommandValidator : AbstractValidator<UpdatePlaylistCommand>
{
    public UpdatePlaylistCommandValidator()
    {
        RuleFor(v => v.Title)
            .NotEmpty()
            .MaximumLength(200);
    }
}
