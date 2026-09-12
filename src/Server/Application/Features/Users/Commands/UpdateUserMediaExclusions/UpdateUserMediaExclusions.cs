using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Users.Commands.UpdateUserMediaExclusions;

[Authorize(Roles = Roles.Administrator)]
public record UpdateUserMediaExclusionsCommand : IRequest
{
    public required Guid Id { get; init; }
    public required IReadOnlyList<Guid> ExcludedMediaIds { get; init; }
}

public class UpdateUserMediaExclusionsCommandHandler(
    IApplicationDbContext context,
    IMediaQueryCacheInvalidator cacheInvalidator,
    IIdentityService identityService)
    : IRequestHandler<UpdateUserMediaExclusionsCommand>
{
    public async Task Handle(UpdateUserMediaExclusionsCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, user);

        var userName = user.IdentityUserId is not null
            ? await identityService.GetUserNameAsync(user.IdentityUserId)
            : null;

        var existing = await context.UserMediaExclusions
            .Where(e => e.UserId == request.Id)
            .ToListAsync(cancellationToken);

        var existingDict = existing.ToDictionary(e => e.MediaId);
        var requestedSet = request.ExcludedMediaIds.ToHashSet();

        foreach (var mediaId in requestedSet)
        {
            if (existingDict.TryGetValue(mediaId, out var row))
            {
                row.IsAdminExcluded = true;
                AddHiddenChangedEvent(row, request.Id, userName);
            }
            else
            {
                var exclusion = new UserMediaExclusion
                {
                    Id = Guid.NewGuid(),
                    UserId = request.Id,
                    MediaId = mediaId,
                    IsAdminExcluded = true
                };
                AddHiddenChangedEvent(exclusion, request.Id, userName);
                context.UserMediaExclusions.Add(exclusion);
            }
        }

        foreach (var row in existing.Where(e => !requestedSet.Contains(e.MediaId)))
        {
            row.IsAdminExcluded = false;
            AddHiddenChangedEvent(row, request.Id, userName);
            if (!row.IsSelfExcluded)
                context.UserMediaExclusions.Remove(row);
        }

        await context.SaveChangesAsync(cancellationToken);
        cacheInvalidator.InvalidateAll();
    }

    private static void AddHiddenChangedEvent(UserMediaExclusion exclusion, Guid userId, string? userName)
    {
        var isHidden = exclusion.IsSelfExcluded || exclusion.IsAdminExcluded;
        exclusion.AddDomainEvent(new MediaHiddenChangedEvent(
            userId,
            userName,
            exclusion.MediaId,
            isHidden,
            exclusion.IsSelfExcluded,
            exclusion.IsAdminExcluded));
    }
}
