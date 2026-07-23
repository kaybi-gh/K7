namespace K7.Server.Application.Features.Federation.Commands.CopyFederatedPlaylist;

public class CopyFederatedPlaylistCommandValidator : AbstractValidator<CopyFederatedPlaylistCommand>
{
    public CopyFederatedPlaylistCommandValidator()
    {
        RuleFor(x => x.PeerServerId).NotEmpty();
        RuleFor(x => x.OriginUserId).NotEmpty();
        RuleFor(x => x.PlaylistId).NotEmpty();
    }
}
