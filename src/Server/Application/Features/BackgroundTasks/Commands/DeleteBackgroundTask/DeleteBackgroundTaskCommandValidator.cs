namespace K7.Server.Application.Features.BackgroundTasks.Commands.DeleteBackgroundTask;

public class DeleteBackgroundTaskCommandValidator : AbstractValidator<DeleteBackgroundTaskCommand>
{
    public DeleteBackgroundTaskCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
