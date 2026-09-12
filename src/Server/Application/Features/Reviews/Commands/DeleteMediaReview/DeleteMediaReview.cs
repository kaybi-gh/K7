using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Reviews;
using K7.Server.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Reviews.Commands.DeleteMediaReview;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record DeleteMediaReviewCommand(Guid MediaId) : IRequest;

public class DeleteMediaReviewCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMediaAccessGuard accessGuard,
    IIdentityService identityService)
    : IRequestHandler<DeleteMediaReviewCommand>
{
    public async Task Handle(DeleteMediaReviewCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            throw new ForbiddenAccessException();

        await accessGuard.EnsureAccessAsync(command.MediaId, cancellationToken);

        var review = await context.MediaReviews
            .FirstOrDefaultAsync(r => r.UserId == userId && r.MediaId == command.MediaId, cancellationToken);

        if (review is null)
            throw new NotFoundException(command.MediaId.ToString(), nameof(MediaReview));

        var userName = currentUser.IdentityId is not null
            ? await identityService.GetUserNameAsync(currentUser.IdentityId)
            : null;

        review.AddDomainEvent(new MediaReviewDeletedEvent(userId, userName, command.MediaId));

        context.MediaReviews.Remove(review);
        await context.SaveChangesAsync(cancellationToken);
    }
}
