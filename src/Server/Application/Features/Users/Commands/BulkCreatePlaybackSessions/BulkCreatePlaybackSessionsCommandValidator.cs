namespace K7.Server.Application.Features.Users.Commands.BulkCreatePlaybackSessions;

public class BulkCreatePlaybackSessionsCommandValidator : AbstractValidator<BulkCreatePlaybackSessionsCommand>
{
    public BulkCreatePlaybackSessionsCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty();

        RuleFor(v => v.Items)
            .NotNull();

        RuleForEach(v => v.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MediaId).NotEmpty();
            item.RuleFor(i => i.DurationSeconds).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.WatchedDurationSeconds).GreaterThanOrEqualTo(0)
                .When(i => i.WatchedDurationSeconds.HasValue);
        });
    }
}
