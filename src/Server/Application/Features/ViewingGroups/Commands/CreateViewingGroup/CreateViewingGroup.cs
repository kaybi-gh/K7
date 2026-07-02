using FluentValidation;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.ViewingGroups.Commands.CreateViewingGroup;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record CreateViewingGroupCommand : IRequest<Guid>
{
    public required string Name { get; init; }
    public required Guid HostUserId { get; init; }
    public required IReadOnlyList<Guid> MemberUserIds { get; init; }
    public string? Pin { get; init; }
}

public class CreateViewingGroupCommandValidator : AbstractValidator<CreateViewingGroupCommand>
{
    public CreateViewingGroupCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MemberUserIds).Must(ids => ids.Count is >= 2 and <= 8);
        RuleFor(x => x).Must(x => x.MemberUserIds.Contains(x.HostUserId))
            .WithMessage("Host must be a group member.");
        RuleFor(x => x.Pin).MaximumLength(20).When(x => x.Pin is not null);
    }
}

public class CreateViewingGroupCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<CreateViewingGroupCommand, Guid>
{
    public async Task<Guid> Handle(CreateViewingGroupCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);

        var memberIds = request.MemberUserIds.Distinct().ToList();
        if (!memberIds.Contains(currentUser.Id.Value))
            throw new ForbiddenAccessException();

        await ViewingGroupMemberValidator.EnsureValidMembersAsync(context, memberIds, cancellationToken);

        var entity = new ViewingGroup
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            HostUserId = request.HostUserId,
            CreatedByUserId = currentUser.Id.Value,
            PinHash = request.Pin is null ? null : PinHashHelper.Hash(request.Pin),
            Members = memberIds.Select(id => new ViewingGroupMember
            {
                ViewingGroupId = default,
                UserId = id
            }).ToList()
        };

        foreach (var member in entity.Members)
            member.ViewingGroupId = entity.Id;

        context.ViewingGroups.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
