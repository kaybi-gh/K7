using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Reviews;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MediaUserStateTransferHelperTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ILogger _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
        _logger = Substitute.For<ILogger>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task TransferAsync_ShouldNoOp_WhenFromEqualsTo()
    {
        var (fromId, _, userId) = await SeedMoviesAsync();
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = fromId,
            PlayCount = 1
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, fromId, _logger);
        await _context.SaveChangesAsync();

        (await _context.UserMediaStates.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task TransferAsync_ShouldMoveUserMediaState_WhenTargetHasNone()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = fromId,
            PlayCount = 2,
            ProgressPercentage = 40,
            LastPlaybackPosition = 90,
            LastInteractedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var state = await _context.UserMediaStates.SingleAsync(s => s.UserId == userId);
        state.MediaId.Should().Be(toId);
        state.PlayCount.Should().Be(2);
        state.ProgressPercentage.Should().Be(40);
    }

    [Test]
    public async Task TransferAsync_ShouldMergeUserMediaState_WhenTargetAlreadyHasState()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = fromId,
            PlayCount = 3,
            SkipCount = 2,
            ProgressPercentage = 80,
            LastPlaybackPosition = 200,
            IsCompleted = true,
            LastInteractedAt = DateTime.UtcNow
        });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = toId,
            PlayCount = 1,
            SkipCount = 4,
            ProgressPercentage = 10,
            LastPlaybackPosition = 20,
            LastInteractedAt = DateTime.UtcNow.AddDays(-2)
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var state = await _context.UserMediaStates.SingleAsync(s => s.UserId == userId);
        state.MediaId.Should().Be(toId);
        state.PlayCount.Should().Be(4);
        state.SkipCount.Should().Be(6);
        state.ProgressPercentage.Should().Be(80);
        state.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task TransferAsync_ShouldMoveCollectionItem_WhenTargetNotInCollection()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Title = "Favorites",
            UserId = userId
        };
        _context.Collections.Add(collection);
        _context.CollectionItems.Add(new CollectionItem
        {
            CollectionId = collection.Id,
            MediaId = fromId,
            Order = 0
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var item = await _context.CollectionItems.SingleAsync();
        item.MediaId.Should().Be(toId);
        item.CollectionId.Should().Be(collection.Id);
    }

    [Test]
    public async Task TransferAsync_ShouldDedupeCollectionItem_WhenTargetAlreadyInCollection()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Title = "Favorites",
            UserId = userId
        };
        _context.Collections.Add(collection);
        _context.CollectionItems.Add(new CollectionItem
        {
            CollectionId = collection.Id,
            MediaId = fromId,
            Order = 0
        });
        _context.CollectionItems.Add(new CollectionItem
        {
            CollectionId = collection.Id,
            MediaId = toId,
            Order = 1
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var items = await _context.CollectionItems.ToListAsync();
        items.Should().ContainSingle();
        items[0].MediaId.Should().Be(toId);
    }

    [Test]
    public async Task TransferAsync_ShouldReparentPlaybackSessions()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            UserId = userId,
            MediaId = fromId,
            SessionId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddHours(-1),
            PositionSeconds = 10,
            DurationSeconds = 100,
            WatchedDurationSeconds = 10,
            State = PlaybackState.Ended
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var session = await _context.MediaPlaybackSessions.SingleAsync();
        session.MediaId.Should().Be(toId);
    }

    [Test]
    public async Task TransferAsync_ShouldMoveExclusion_WhenTargetHasNone()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        _context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            UserId = userId,
            MediaId = fromId,
            IsSelfExcluded = true
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var exclusion = await _context.UserMediaExclusions.SingleAsync();
        exclusion.MediaId.Should().Be(toId);
        exclusion.IsSelfExcluded.Should().BeTrue();
    }

    [Test]
    public async Task TransferAsync_ShouldMergeExclusionFlags_WhenTargetAlreadyExcluded()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        _context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            UserId = userId,
            MediaId = fromId,
            IsAdminExcluded = true,
            IsSelfExcluded = false
        });
        _context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            UserId = userId,
            MediaId = toId,
            IsAdminExcluded = false,
            IsSelfExcluded = true
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var exclusion = await _context.UserMediaExclusions.SingleAsync();
        exclusion.MediaId.Should().Be(toId);
        exclusion.IsAdminExcluded.Should().BeTrue();
        exclusion.IsSelfExcluded.Should().BeTrue();
    }

    [Test]
    public async Task TransferAsync_ShouldMovePlaylistItem_WhenTargetNotOnPlaylist()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Title = "Watch later",
            UserId = userId,
            MediaType = MediaType.Movie
        };
        _context.Playlists.Add(playlist);
        _context.PlaylistItems.Add(new PlaylistItem
        {
            PlaylistId = playlist.Id,
            MediaId = fromId,
            Order = 0
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var item = await _context.PlaylistItems.SingleAsync();
        item.MediaId.Should().Be(toId);
    }

    [Test]
    public async Task TransferAsync_ShouldMoveReview_WhenTargetHasNone()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        var rating = new UserRating
        {
            Id = Guid.NewGuid(),
            MediaId = fromId,
            UserId = userId,
            Value = 8,
            MinimumValue = 0,
            MaximumValue = 10
        };
        _context.Ratings.Add(rating);
        _context.MediaReviews.Add(new MediaReview
        {
            MediaId = fromId,
            UserId = userId,
            UserRatingId = rating.Id,
            Text = "Great"
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var review = await _context.MediaReviews.SingleAsync();
        review.MediaId.Should().Be(toId);
    }

    [Test]
    public async Task TransferAsync_ShouldMoveUserRating_WhenTargetHasNone()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        _context.Ratings.Add(new UserRating
        {
            MediaId = fromId,
            UserId = userId,
            Value = 8,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var rating = await _context.Ratings.OfType<UserRating>().SingleAsync();
        rating.MediaId.Should().Be(toId);
        rating.Value.Should().Be(8);
    }

    [Test]
    public async Task TransferAsync_ShouldDedupePlaylistItem_WhenTargetAlreadyOnPlaylist()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Title = "Watch later",
            UserId = userId,
            MediaType = MediaType.Movie
        };
        _context.Playlists.Add(playlist);
        _context.PlaylistItems.Add(new PlaylistItem
        {
            PlaylistId = playlist.Id,
            MediaId = fromId,
            Order = 0
        });
        _context.PlaylistItems.Add(new PlaylistItem
        {
            PlaylistId = playlist.Id,
            MediaId = toId,
            Order = 1
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var items = await _context.PlaylistItems.ToListAsync();
        items.Should().ContainSingle();
        items[0].MediaId.Should().Be(toId);
    }

    [Test]
    public async Task TransferAsync_ShouldMoveCollectionItemsIndependently_WhenMultipleCollections()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        var collectionA = new Collection { Id = Guid.NewGuid(), Title = "A", UserId = userId };
        var collectionB = new Collection { Id = Guid.NewGuid(), Title = "B", UserId = userId };
        _context.Collections.AddRange(collectionA, collectionB);
        _context.CollectionItems.Add(new CollectionItem
        {
            CollectionId = collectionA.Id,
            MediaId = fromId,
            Order = 0
        });
        _context.CollectionItems.Add(new CollectionItem
        {
            CollectionId = collectionB.Id,
            MediaId = fromId,
            Order = 0
        });
        _context.CollectionItems.Add(new CollectionItem
        {
            CollectionId = collectionB.Id,
            MediaId = toId,
            Order = 1
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var items = await _context.CollectionItems.OrderBy(i => i.CollectionId).ToListAsync();
        items.Should().HaveCount(2);
        items.Should().Contain(i => i.CollectionId == collectionA.Id && i.MediaId == toId);
        items.Should().Contain(i => i.CollectionId == collectionB.Id && i.MediaId == toId);
    }

    private async Task<(Guid FromId, Guid ToId, Guid UserId)> SeedMoviesAsync()
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/media/movies",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });

        var from = new Movie { Id = Guid.NewGuid(), Title = "Wrong Title" };
        var to = new Movie { Id = Guid.NewGuid(), Title = "Correct Title" };
        _context.Medias.AddRange(from, to);
        await _context.SaveChangesAsync();
        return (from.Id, to.Id, userId);
    }
}
