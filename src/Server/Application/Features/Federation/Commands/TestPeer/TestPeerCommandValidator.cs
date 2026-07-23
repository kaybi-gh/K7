namespace K7.Server.Application.Features.Federation.Commands.TestPeer;

public class TestPeerCommandValidator : AbstractValidator<TestPeerCommand>
{
    public TestPeerCommandValidator()
    {
        RuleFor(x => x.PeerId).NotEmpty();
    }
}
