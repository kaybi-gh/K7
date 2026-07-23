namespace K7.Server.Application.Features.Federation.Commands.SyncPeerMetadata;

public class SyncPeerMetadataCommandValidator : AbstractValidator<SyncPeerMetadataCommand>
{
    public SyncPeerMetadataCommandValidator()
    {
        RuleFor(x => x.PeerId).NotEmpty();
    }
}
