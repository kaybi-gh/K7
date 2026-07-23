namespace K7.Server.Application.Features.Reviews.Commands.DeleteMediaReview;

public class DeleteMediaReviewCommandValidator : AbstractValidator<DeleteMediaReviewCommand>
{
    public DeleteMediaReviewCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
    }
}
