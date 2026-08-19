namespace K7.Server.Application.Features.Stats.Commands.ReassignPlaybackHistoryItem;

public class ReassignPlaybackHistoryItemCommandValidator : AbstractValidator<ReassignPlaybackHistoryItemCommand>
{
    public ReassignPlaybackHistoryItemCommandValidator()
    {
        RuleFor(v => v.ReferenceId).NotEmpty();
    }
}
