namespace K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;

public class CreateBackgroundTaskCommandValidator : AbstractValidator<CreateBackgroundTaskCommand>
{
    public CreateBackgroundTaskCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.TargetEntityTypeName).MaximumLength(500);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.MaxAttempts).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TimeoutSeconds).GreaterThan(0).When(x => x.TimeoutSeconds.HasValue);
        RuleFor(x => x.ConcurrencyGroup).MaximumLength(500);
    }
}
