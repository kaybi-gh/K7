using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Services;
using K7.Server.Application.UnitTests.Features.MediaShares;
using K7.Server.Domain.Entities.Shares;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.UnitTests.Services;

/// <summary>
/// A public share visitor must only reach what one of its shares covers, whatever the
/// regular user rules would allow.
/// </summary>
[TestFixture]
public class MediaAccessGuardShareVisitorTests
{
    private MediaShareTestDatabase _database = null!;
    private IShareVisitorContext _visitorContext = null!;
    private IUser _currentUser = null!;
    private MediaAccessGuard _guard = null!;
    private MediaShare _share = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new MediaShareTestDatabase();
        _share = _database.AddShare(_database.Season1Id, "token");

        _visitorContext = Substitute.For<IShareVisitorContext>();
        _visitorContext.IsShareVisitor.Returns(true);
        _visitorContext.Grants.Returns([new ShareVisitorGrant(_share.Id, Guid.NewGuid())]);

        // The technical visitor account: no exclusion of its own, so only the share limits it.
        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(Guid.NewGuid());
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var shareVisitorAccess = new ShareVisitorAccess(
            _database.Context,
            _visitorContext,
            new ShareScopeResolver(_database.Context));
        _guard = new MediaAccessGuard(
            _database.Context,
            _currentUser,
            new MediaAccessFilter(_database.Context),
            shareVisitorAccess,
            _visitorContext,
            new ShareAccessInvalidator(),
            new BoundedMemoryCache());
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    [Test]
    public async Task EnsureAccessAsync_ShouldAllow_WhenMediaIsInTheShare()
    {
        var act = () => _guard.EnsureAccessAsync(_database.Episode1Id);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task EnsureAccessAsync_ShouldThrowNotFound_WhenMediaIsAboveTheShareRoot()
    {
        var act = () => _guard.EnsureAccessAsync(_database.SerieId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task EnsureAccessAsync_ShouldThrowNotFound_WhenMediaIsOutsideTheShare()
    {
        var act = () => _guard.EnsureAccessAsync(_database.MovieId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task EnsureAccessAsync_ShouldThrowNotFound_WhenVisitorHasNoUserId()
    {
        // A request without a user id is let through for regular accounts. Never for a visitor.
        _currentUser.Id.Returns((Guid?)null);

        var act = () => _guard.EnsureAccessAsync(_database.MovieId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task EnsureAccessAsync_ShouldThrowNotFound_WhenVisitorHasNoGrant()
    {
        _visitorContext.Grants.Returns([]);

        var act = () => _guard.EnsureAccessAsync(_database.Episode1Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task EnsureAccessAsync_ShouldThrowNotFound_WhenShareIsRevoked()
    {
        var share = await _database.Context.MediaShares.FindAsync(_share.Id);
        share!.RevokedAt = DateTimeOffset.UtcNow;
        await _database.Context.SaveChangesAsync();

        var act = () => _guard.EnsureAccessAsync(_database.Episode1Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task EnsureAccessAsync_ShouldThrowNotFound_WhenCreatorCannotSeeTheMedia()
    {
        // Sharing never gives more than its creator has.
        _database.Context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            UserId = _database.CreatorId,
            MediaId = _database.Episode1Id,
            IsAdminExcluded = true
        });
        await _database.Context.SaveChangesAsync();

        var act = () => _guard.EnsureAccessAsync(_database.Episode1Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task EnsureAccessByIndexedFileAsync_ShouldAllow_WhenFileBelongsToASharedMedia()
    {
        var act = () => _guard.EnsureAccessByIndexedFileAsync(_database.Episode1FileId);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task EnsureAccessByIndexedFileAsync_ShouldThrowNotFound_WhenFileIsOutsideTheShare()
    {
        var act = () => _guard.EnsureAccessByIndexedFileAsync(_database.MovieFileId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task EnsureAccessByIndexedFileAsync_ShouldThrowNotFound_WhenFileHasNoMedia()
    {
        var act = () => _guard.EnsureAccessByIndexedFileAsync(_database.OrphanFileId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task EnsureAccessAsync_ShouldUseRegularRules_WhenRequestIsNotAShareVisitor()
    {
        _visitorContext.IsShareVisitor.Returns(false);

        var act = () => _guard.EnsureAccessAsync(_database.MovieId);

        await act.Should().NotThrowAsync();
    }
}
