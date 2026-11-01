using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using K7.Shared.Dtos.Entities.Reviews;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class MediaReviewsSectionTests
{
    private static readonly Guid MediaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Test]
    public void Render_ShouldSkipSpoilerLookups_WhenThereIsNoReview()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var (reviews, media, users) = RegisterServices(ctx, []);

        ctx.Render<MediaReviewsSection>(p => p.Add(x => x.MediaId, MediaId));

        reviews.Received(1).GetMediaReviewsAsync(MediaId, Arg.Any<CancellationToken>());
        users.DidNotReceive().GetCurrentUserAsync(Arg.Any<CancellationToken>());
        reviews.DidNotReceive().GetReviewPreferencesAsync(Arg.Any<CancellationToken>());
        media.DidNotReceiveWithAnyArgs().GetMediaAsync(default, default, default);
    }

    [Test]
    public void Render_ShouldUseTheWatchedParameter_WhenReviewsNeedSpoilerBlur()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var review = new MediaReviewDto
        {
            Id = Guid.NewGuid(),
            MediaId = MediaId,
            UserId = Guid.NewGuid(),
            UserRatingId = Guid.NewGuid(),
            Rating = 8,
            UserDisplayName = "Alice",
            Text = "Great",
            Created = DateTimeOffset.UtcNow
        };
        var (reviews, media, _) = RegisterServices(ctx, [review]);
        reviews.GetReviewPreferencesAsync(Arg.Any<CancellationToken>())
            .Returns(new ReviewPreferencesDto { BlurReviewsForUnwatchedMedia = true });

        ctx.Render<MediaReviewsSection>(p => p
            .Add(x => x.MediaId, MediaId)
            .Add(x => x.Watched, false));

        reviews.Received(1).GetReviewPreferencesAsync(Arg.Any<CancellationToken>());
        media.DidNotReceiveWithAnyArgs().GetMediaAsync(default, default, default);
    }

    private static (IReviewService Reviews, IMediaService Media, IUserAdminService Users) RegisterServices(
        BunitContext ctx,
        IReadOnlyList<MediaReviewDto> localReviews)
    {
        var reviews = Substitute.For<IReviewService>();
        reviews.GetMediaReviewsAsync(MediaId, Arg.Any<CancellationToken>()).Returns(localReviews);
        reviews.GetFederatedMediaReviewsAsync(MediaId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FederatedReviewDto>)[]);
        reviews.GetReviewPreferencesAsync(Arg.Any<CancellationToken>()).Returns(new ReviewPreferencesDto());

        var media = Substitute.For<IMediaService>();
        var users = Substitute.For<IUserAdminService>();

        ctx.Services.AddLocalization();
        ctx.Services.AddSingleton(reviews);
        ctx.Services.AddSingleton(media);
        ctx.Services.AddSingleton(users);
        ctx.Services.AddSingleton(Substitute.For<IK7DialogService>());
        ctx.Services.AddSingleton(Substitute.For<IK7ServerService>());
        // Review cards pull avatar, rating and menu services this test does not exercise.
        ctx.Services.AddFallbackServiceProvider(new SubstituteServiceProvider());
        return (reviews, media, users);
    }

    private sealed class SubstituteServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType.IsInterface && serviceType.Namespace?.StartsWith("K7.", StringComparison.Ordinal) == true
                ? Substitute.For([serviceType], [])
                : null;
    }
}
