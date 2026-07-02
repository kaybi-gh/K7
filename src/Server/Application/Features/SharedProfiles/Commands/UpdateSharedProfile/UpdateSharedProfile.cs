using FluentValidation;
using FluentValidation.Results;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfile;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UpdateSharedProfileCommand : IRequest
{
    public required Guid Id { get; init; }
    public string? Name { get; init; }
    public Guid? HostUserId { get; init; }
    public IReadOnlyList<Guid>? MemberUserIds { get; init; }
}

public class UpdateSharedProfileCommandValidator : AbstractValidator<UpdateSharedProfileCommand>
{
    public UpdateSharedProfileCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.MemberUserIds!).Must(ids => ids.Count is >= 2 and <= 8)
            .When(x => x.MemberUserIds is not null);
    }
}

public class UpdateSharedProfileCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<UpdateSharedProfileCommand>
{
    public async Task Handle(UpdateSharedProfileCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);

        var group = await SharedProfileMemberValidator.GetGroupForMemberAsync(
            context, request.Id, currentUser.Id.Value, cancellationToken);

        if (request.Name is not null)
            group.Name = request.Name.Trim();

        if (request.MemberUserIds is not null)
        {
            var memberIds = request.MemberUserIds.Distinct().ToList();
            var hostUserId = request.HostUserId ?? group.HostUserId;

            if (!memberIds.Contains(hostUserId))
            {
                throw new ValidationException(
                [
                    new ValidationFailure(nameof(request.HostUserId), "Host must be a group member.")
                ]);
            }

            await SharedProfileMemberValidator.EnsureValidMembersAsync(context, memberIds, currentUser.Id.Value, cancellationToken);

            group.HostUserId = hostUserId;
            context.SharedProfileMembers.RemoveRange(group.Members);
            group.Members = memberIds.Select(id => new SharedProfileMember
            {
                SharedProfileId = group.Id,
                UserId = id
            }).ToList();
        }
        else if (request.HostUserId is { } hostUserId)
        {
            if (!group.Members.Any(m => m.UserId == hostUserId))
            {
                throw new ValidationException(
                [
                    new ValidationFailure(nameof(request.HostUserId), "Host must be a group member.")
                ]);
            }

            group.HostUserId = hostUserId;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
