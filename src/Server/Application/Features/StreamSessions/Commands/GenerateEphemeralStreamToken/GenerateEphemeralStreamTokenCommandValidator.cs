namespace K7.Server.Application.Features.StreamSessions.Commands.GenerateEphemeralStreamToken;

public class GenerateEphemeralStreamTokenCommandValidator : AbstractValidator<GenerateEphemeralStreamTokenCommand>
{
    public GenerateEphemeralStreamTokenCommandValidator()
    {
        RuleFor(x => x.StreamSessionId).NotEmpty();
    }
}
