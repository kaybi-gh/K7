namespace K7.Server.Application.Features.Users.Commands.BulkUpsertMediaStates;

public class BulkUpsertMediaStatesCommandValidator : AbstractValidator<BulkUpsertMediaStatesCommand>
{
    public BulkUpsertMediaStatesCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty();

        RuleFor(v => v.Items)
            .NotNull();

        RuleForEach(v => v.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MediaId).NotEmpty();
            item.RuleFor(i => i.PlayCount).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.ProgressPercentage).InclusiveBetween(0, 100);
        });
    }
}
