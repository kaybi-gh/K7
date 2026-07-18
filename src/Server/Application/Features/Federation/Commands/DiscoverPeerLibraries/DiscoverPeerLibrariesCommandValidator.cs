namespace K7.Server.Application.Features.Federation.Commands.DiscoverPeerLibraries;

public class DiscoverPeerLibrariesCommandValidator : AbstractValidator<DiscoverPeerLibrariesCommand>
{
    public DiscoverPeerLibrariesCommandValidator()
    {
        RuleFor(x => x.PeerId).NotEmpty();
    }
}
