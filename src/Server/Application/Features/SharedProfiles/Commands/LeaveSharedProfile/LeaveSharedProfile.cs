using FluentValidation.Results;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.SharedProfiles.Commands.LeaveSharedProfile;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record LeaveSharedProfileCommand(Guid Id, Guid? NewHostUserId) : IRequest;

public class LeaveSharedProfileCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<LeaveSharedProfileCommand>
{
    public async Task Handle(LeaveSharedProfileCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);
        var userId = currentUser.Id.Value;

        var group = await SharedProfileMemberValidator.GetGroupForMemberAsync(
            context, request.Id, userId, cancellationToken);

        if (group.Members.Count <= SharedProfileMemberValidator.MinMembers)
        {
            context.SharedProfiles.Remove(group);
        }
        else
        {
            var member = group.Members.First(m => m.UserId == userId);
            context.SharedProfileMembers.Remove(member);
            group.Members.Remove(member);

            if (group.HostUserId == userId)
            {
                if (request.NewHostUserId is null)
                {
                    throw new ValidationException(
                    [
                        new ValidationFailure(nameof(request.NewHostUserId), "A new host must be selected when leaving as host.")
                    ]);
                }

                if (!group.Members.Any(m => m.UserId == request.NewHostUserId))
                {
                    throw new ValidationException(
                    [
                        new ValidationFailure(nameof(request.NewHostUserId), "New host must be a group member.")
                    ]);
                }

                group.HostUserId = request.NewHostUserId.Value;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
