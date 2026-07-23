namespace K7.Server.Application.Features.StreamSessions.Commands.RevokeEphemeralStreamToken;

public class RevokeEphemeralStreamTokenCommandValidator : AbstractValidator<RevokeEphemeralStreamTokenCommand>
{
    public RevokeEphemeralStreamTokenCommandValidator()
    {
        RuleFor(x => x.StreamSessionId).NotEmpty();
    }
}
