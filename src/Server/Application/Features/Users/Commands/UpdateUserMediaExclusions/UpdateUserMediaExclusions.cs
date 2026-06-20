using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.Users.Commands.UpdateUserMediaExclusions;

[Authorize(Roles = Roles.Administrator)]
public record UpdateUserMediaExclusionsCommand : IRequest
{
    public required Guid Id { get; init; }
    public required IReadOnlyList<Guid> ExcludedMediaIds { get; init; }
}

public class UpdateUserMediaExclusionsCommandHandler(
    IApplicationDbContext context,
    IMediaQueryCacheInvalidator cacheInvalidator)
    : IRequestHandler<UpdateUserMediaExclusionsCommand>
{
    public async Task Handle(UpdateUserMediaExclusionsCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, user);

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
            }
            else
            {
                context.UserMediaExclusions.Add(new UserMediaExclusion
                {
                    Id = Guid.NewGuid(),
                    UserId = request.Id,
                    MediaId = mediaId,
                    IsAdminExcluded = true
                });
            }
        }

        foreach (var row in existing.Where(e => !requestedSet.Contains(e.MediaId)))
        {
            row.IsAdminExcluded = false;
            if (!row.IsSelfExcluded)
                context.UserMediaExclusions.Remove(row);
        }

        await context.SaveChangesAsync(cancellationToken);
        cacheInvalidator.InvalidateAll();
    }
}
