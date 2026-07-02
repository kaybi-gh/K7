using FluentValidation;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.SharedProfiles.Commands.CreateSharedProfile;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record CreateSharedProfileCommand : IRequest<Guid>
{
    public required string Name { get; init; }
    public required Guid HostUserId { get; init; }
    public required IReadOnlyList<Guid> MemberUserIds { get; init; }
    public string? Pin { get; init; }
}

public class CreateSharedProfileCommandValidator : AbstractValidator<CreateSharedProfileCommand>
{
    public CreateSharedProfileCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MemberUserIds).Must(ids => ids.Count is >= 2 and <= 8);
        RuleFor(x => x).Must(x => x.MemberUserIds.Contains(x.HostUserId))
            .WithMessage("Host must be a group member.");
        RuleFor(x => x.Pin).MaximumLength(20).When(x => x.Pin is not null);
    }
}

public class CreateSharedProfileCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<CreateSharedProfileCommand, Guid>
{
    public async Task<Guid> Handle(CreateSharedProfileCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);

        var memberIds = request.MemberUserIds.Distinct().ToList();
        if (!memberIds.Contains(currentUser.Id.Value))
            throw new ForbiddenAccessException();

        await SharedProfileMemberValidator.EnsureValidMembersAsync(context, memberIds, currentUser.Id.Value, cancellationToken);

        var entity = new SharedProfile
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            HostUserId = request.HostUserId,
            CreatedByUserId = currentUser.Id.Value,
            PinHash = request.Pin is null ? null : PinHashHelper.Hash(request.Pin),
            Members = memberIds.Select(id => new SharedProfileMember
            {
                SharedProfileId = default,
                UserId = id
            }).ToList()
        };

        foreach (var member in entity.Members)
            member.SharedProfileId = entity.Id;

        context.SharedProfiles.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
