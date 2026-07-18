namespace K7.Server.Application.Features.Reviews.Commands.UpsertMediaReview;

public class UpsertMediaReviewCommandValidator : AbstractValidator<UpsertMediaReviewCommand>
{
    public UpsertMediaReviewCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
    }
}
