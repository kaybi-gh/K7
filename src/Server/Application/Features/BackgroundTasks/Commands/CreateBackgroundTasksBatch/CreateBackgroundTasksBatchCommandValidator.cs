namespace K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;

public class CreateBackgroundTasksBatchCommandValidator : AbstractValidator<CreateBackgroundTasksBatchCommand>
{
    public CreateBackgroundTasksBatchCommandValidator()
    {
        RuleFor(v => v.Items)
            .NotNull();

        RuleForEach(v => v.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Request).NotNull();
            item.RuleFor(i => i.Lane).IsInEnum();
            item.RuleFor(i => i.WorkClass).IsInEnum();
            item.RuleFor(i => i.TriggeredBy).IsInEnum();
            item.RuleFor(i => i.MaxAttempts).GreaterThan(0);
            item.RuleFor(i => i.TimeoutSeconds).GreaterThan(0).When(i => i.TimeoutSeconds is not null);
        });
    }
}
