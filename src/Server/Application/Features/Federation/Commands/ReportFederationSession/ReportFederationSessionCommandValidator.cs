namespace K7.Server.Application.Features.Federation.Commands.ReportFederationSession;

public class ReportFederationSessionCommandValidator : AbstractValidator<ReportFederationSessionCommand>
{
    public ReportFederationSessionCommandValidator()
    {
        RuleFor(x => x.ClientId).MaximumLength(500);
        RuleFor(x => x.Request).NotNull();
    }
}
