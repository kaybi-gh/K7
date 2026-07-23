namespace K7.Server.Application.Features.Diagnostics.Commands.QueueDiagnosticFixes;

public class QueueDiagnosticFixesCommandValidator : AbstractValidator<QueueDiagnosticFixesCommand>
{
    public QueueDiagnosticFixesCommandValidator()
    {
        RuleFor(x => x.Issue).IsInEnum();
        RuleFor(x => x.LibraryId).NotEqual(Guid.Empty).When(x => x.LibraryId.HasValue);
    }
}
