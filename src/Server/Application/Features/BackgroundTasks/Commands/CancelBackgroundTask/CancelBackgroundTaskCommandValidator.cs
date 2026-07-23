namespace K7.Server.Application.Features.BackgroundTasks.Commands.CancelBackgroundTask;

public class CancelBackgroundTaskCommandValidator : AbstractValidator<CancelBackgroundTaskCommand>
{
    public CancelBackgroundTaskCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
