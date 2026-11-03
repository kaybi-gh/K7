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
            Substitute.For<IDatabaseCapabilities>());
    }
}
