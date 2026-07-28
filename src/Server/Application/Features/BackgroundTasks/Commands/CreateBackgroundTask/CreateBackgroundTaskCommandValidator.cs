namespace K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;

public class CreateBackgroundTaskCommandValidator : AbstractValidator<CreateBackgroundTaskCommand>
{
    public CreateBackgroundTaskCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.TargetEntityTypeName).MaximumLength(500);
        RuleFor(x => x.Lane).IsInEnum();
        RuleFor(x => x.WorkClass).IsInEnum();
        RuleFor(x => x.TriggeredBy).IsInEnum();
        RuleFor(x => x.MaxAttempts).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TimeoutSeconds).GreaterThan(0).When(x => x.TimeoutSeconds.HasValue);
    }
}
