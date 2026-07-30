using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Reviews;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Reviews.Queries.GetMyMediaReviews;

[Authorize]
public record GetMyMediaReviewsQuery : IRequest<IReadOnlyList<SocialUserReviewViewDto>>;

public class GetMyMediaReviewsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetMyMediaReviewsQuery, IReadOnlyList<SocialUserReviewViewDto>>
{
    private const int MaxItems = 500;

    public async Task<IReadOnlyList<SocialUserReviewViewDto>> Handle(
        GetMyMediaReviewsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return [];

        var reviews = await context.MediaReviews
            .AsNoTracking()
            .IncludeReviewMediaDetails()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Created)
            .Take(MaxItems)
            .ToListAsync(cancellationToken);

        var results = reviews.Select(r => new SocialUserReviewViewDto
        {
            Id = r.Id,
            Text = r.Text,
            Emoji = r.Emoji,
            Rating = (int)(r.UserRating?.Value ?? 0),
            Created = r.Created,
            Media = r.Media!.ToSocialUserMediaCard(FederatedSocialItemStatus.ResolvedLocal)
        }).ToList();

        var mediaIds = results
            .Where(r => r.Media.LocalMediaId is not null && r.Media.CoverPictureId is null)
            .Select(r => r.Media.LocalMediaId!.Value)
            .Distinct()
            .ToList();

        if (mediaIds.Count == 0)
            return results;

        var coverPictureIds = await MediaCoverPictureResolver.GetCoverPictureIdsByMediaIdAsync(
            context,
            mediaIds,
            cancellationToken);

        for (var i = 0; i < results.Count; i++)
        {
            var media = results[i].Media;
            if (media.CoverPictureId is not null || media.LocalMediaId is not Guid mediaId)
                continue;

            if (!coverPictureIds.TryGetValue(mediaId, out var coverPictureId) || coverPictureId is null)
                continue;

            results[i] = results[i] with { Media = media with { CoverPictureId = coverPictureId } };
        }

        return results;
    }
}
