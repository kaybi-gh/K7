using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Shares;

namespace K7.Server.Application.Features.MediaShares.Queries.GetMediaShares;

/// <summary>
/// Shares the current user manages. Administrators can ask for everyone's shares.
/// </summary>
[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetMediaSharesQuery : IRequest<List<MediaShareDto>>
{
    /// <summary>Administrators only: list the shares of every user.</summary>
    public bool All { get; init; }

    /// <summary>Only the shares whose root is this media.</summary>
    public Guid? MediaId { get; init; }
}

public class GetMediaSharesQueryHandler(
    IApplicationDbContext context,
    IIdentityService identityService,
    IMediaShareManager shareManager)
    : IRequestHandler<GetMediaSharesQuery, List<MediaShareDto>>
{
    public async Task<List<MediaShareDto>> Handle(GetMediaSharesQuery request, CancellationToken cancellationToken)
    {
        var actor = await shareManager.RequireActorAsync(cancellationToken);

        var query = context.MediaShares.AsNoTracking();
        if (!(request.All && actor.IsAdmin))
            query = query.Where(s => s.CreatedByUserId == actor.UserId);

        if (request.MediaId is { } mediaId)
            query = query.Where(s => s.RootMediaId == mediaId);

        var shares = await query
            .OrderByDescending(s => s.Created)
            .ToListAsync(cancellationToken);

        return await MediaShareDtoBuilder.BuildAsync(context, identityService, shareManager, shares, cancellationToken);
    }
}
