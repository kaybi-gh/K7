namespace K7.Server.Application.Features.Users.Commands.BulkUpsertRatings;

public class BulkUpsertRatingsCommandValidator : AbstractValidator<BulkUpsertRatingsCommand>
{
    public BulkUpsertRatingsCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty();

        RuleFor(v => v.Items)
            .NotNull();

        RuleForEach(v => v.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MediaId).NotEmpty();
            item.RuleFor(i => i.Value).InclusiveBetween(0, 10);
        });
    }
}
