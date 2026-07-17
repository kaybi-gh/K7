namespace K7.Server.Application.Features.Playlists.Commands.UploadPlaylistCover;

public class UploadPlaylistCoverCommandValidator : AbstractValidator<UploadPlaylistCoverCommand>
{
    public UploadPlaylistCoverCommandValidator()
    {
        RuleFor(x => x.PlaylistId).NotEmpty();
        RuleFor(x => x.FileName).MaximumLength(500);
    }
}
