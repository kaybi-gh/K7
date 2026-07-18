namespace K7.Server.Application.Features.Federation.Commands.AcceptPeerRequest;

public class AcceptPeerRequestCommandValidator : AbstractValidator<AcceptPeerRequestCommand>
{
    public AcceptPeerRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.SharedLibraryIds).NotNull();
        RuleForEach(x => x.SharedLibraryIds).NotEmpty();
        RuleFor(x => x.MaxConcurrentStreams).GreaterThan(0).When(x => x.MaxConcurrentStreams.HasValue);
    }
}
