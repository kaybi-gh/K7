using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Medias.Queries.GetMedias;

namespace K7.Server.Application.UnitTests.Features.Medias.Queries;

[TestFixture]
public class GetMediasWithPaginationCacheKeyTests
{
    [Test]
    public async Task BuildCacheKeyAsync_ShouldDiffer_WhenSharedProfileChanged()
    {
        var userId = Guid.NewGuid();
        var request = new GetMediasWithPaginationQuery { PageNumber = 1, PageSize = 20 };

        var personalKey = await CreateHandler(sharedProfileId: null)
            .BuildCacheKeyAsync(request, userId, filter: null, includePagination: true, CancellationToken.None);
        var sharedKey = await CreateHandler(sharedProfileId: Guid.NewGuid())
            .BuildCacheKeyAsync(request, userId, filter: null, includePagination: true, CancellationToken.None);

        // The shared profile applies its own restriction profile and age gating.
        sharedKey.Should().NotBe(personalKey);
    }

    [Test]
    public async Task BuildCacheKeyAsync_ShouldKeepPagesWithCardDetailsApart()
    {
        var userId = Guid.NewGuid();
        var plain = new GetMediasWithPaginationQuery { PageNumber = 1, PageSize = 20 };
        var handler = CreateHandler(sharedProfileId: null);

        var plainKey = await handler.BuildCacheKeyAsync(plain, userId, filter: null, includePagination: true, CancellationToken.None);
        var detailedKey = await handler.BuildCacheKeyAsync(
            plain with { IncludeCardDetails = true }, userId, filter: null, includePagination: true, CancellationToken.None);
        var plainCount = await handler.BuildCacheKeyAsync(plain, userId, filter: null, includePagination: false, CancellationToken.None);
        var detailedCount = await handler.BuildCacheKeyAsync(
            plain with { IncludeCardDetails = true }, userId, filter: null, includePagination: false, CancellationToken.None);

        // A page with details must not be served to a plain query, and the other way round.
        detailedKey.Should().NotBe(plainKey);
        // The count does not depend on the details: both share it.
        detailedCount.Should().Be(plainCount);
    }

    private static GetMediasQueryHandler CreateHandler(Guid? sharedProfileId)
    {
        var currentUser = Substitute.For<IUser>();
        currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns(sharedProfileId);

        // The cache key only reads the current user (and the playback policy for continue watching).
        return new GetMediasQueryHandler(
            Substitute.For<IApplicationDbContext>(),
            currentUser,
            Substitute.For<IBoundedMemoryCache>(),
            Substitute.For<IMediaQueryCacheInvalidator>(),
            null!,
            Substitute.For<IPlaybackPolicySettingsProvider>(),
            null!,
            null!,
            Substitute.For<IDatabaseCapabilities>());
    }
}
