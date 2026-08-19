namespace K7.Server.Application.Features.Stats.Commands.DeletePlaybackHistoryItem;

public class DeletePlaybackHistoryItemCommandValidator : AbstractValidator<DeletePlaybackHistoryItemCommand>
{
    public DeletePlaybackHistoryItemCommandValidator()
    {
        RuleFor(v => v.ReferenceId).NotEmpty();
    }
}
