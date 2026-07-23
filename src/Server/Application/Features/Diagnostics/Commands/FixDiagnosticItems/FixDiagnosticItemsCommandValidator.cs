namespace K7.Server.Application.Features.Diagnostics.Commands.FixDiagnosticItems;

public class FixDiagnosticItemsCommandValidator : AbstractValidator<FixDiagnosticItemsCommand>
{
    public FixDiagnosticItemsCommandValidator()
    {
        RuleFor(x => x.EntityIds).NotNull();
        RuleForEach(x => x.EntityIds).NotEmpty();
        RuleFor(x => x.Action).IsInEnum();
    }
}
