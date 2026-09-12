using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class MediaAccessGuardTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private MediaAccessFilter _filter = null!;
    private MediaAccessGuard _guard = null!;

    private Guid _userId;
    private Guid _accessibleMediaId;
    private Guid _excludedMediaId;
    private Guid _libraryId;

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

        _userId = Guid.NewGuid();
        _accessibleMediaId = Guid.NewGuid();
        _excludedMediaId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();

        var groupId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, DisplayName = "viewer" });
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Movie,
            Title = "Movies",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.Medias.AddRange(
            new Movie { Id = _accessibleMediaId, Title = "Ok" },
            new Movie { Id = _excludedMediaId, Title = "Hidden" });
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = _accessibleMediaId, LibraryId = _libraryId },
            new MediaLibraryAvailability { MediaId = _excludedMediaId, LibraryId = _libraryId });
        _context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            UserId = _userId,
            MediaId = _excludedMediaId,
            IsSelfExcluded = true
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);
        _filter = new MediaAccessFilter(_context);
        _guard = new MediaAccessGuard(_context, _currentUser, _filter);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task CanAccessAsync_ShouldReturnTrue_WhenMediaIsAccessible()
    {
        var canAccess = await _guard.CanAccessAsync(_accessibleMediaId, _userId);

        canAccess.Should().BeTrue();
    }

    [Test]
    public async Task CanAccessAsync_ShouldReturnFalse_WhenMediaIsUserExcluded()
    {
        var canAccess = await _guard.CanAccessAsync(_excludedMediaId, _userId);

        canAccess.Should().BeFalse();
    }

    [Test]
    public async Task EnsureAccessAsync_ShouldThrowNotFound_WhenAccessDenied()
    {
        var act = () => _guard.EnsureAccessAsync(_excludedMediaId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task EnsureAccessAsync_ShouldNoOp_WhenCurrentUserHasNoId()
    {
        _currentUser.Id.Returns((Guid?)null);

        await _guard.EnsureAccessAsync(_excludedMediaId);
    }

    [Test]
    public async Task CanAccessAsync_ShouldApplyPersonalRestrictionProfile_WhenNoSharedProfileActive()
    {
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);
        var restrictedProfile = CreateTitleRestrictionProfile("Personal restriction", "Ok");
        _context.ContentRestrictionProfiles.Add(restrictedProfile);
        restrictedProfile.Users.Add(await _context.Users.SingleAsync(u => u.Id == _userId));
        await _context.SaveChangesAsync();

        var canAccess = await _guard.CanAccessAsync(_accessibleMediaId, _userId);

        canAccess.Should().BeFalse();
    }

    [Test]
    public async Task CanAccessAsync_ShouldApplySharedProfileRestrictionProfile_InsteadOfPersonalProfile_WhenSharedProfileActive()
    {
        var sharedProfileId = Guid.NewGuid();
        var restrictedProfile = CreateTitleRestrictionProfile("Shared restriction", "Ok");
        _context.ContentRestrictionProfiles.Add(restrictedProfile);
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Family",
            HostUserId = _userId,
            CreatedByUserId = _userId,
            ContentRestrictionProfile = restrictedProfile
        });
        await _context.SaveChangesAsync();

        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns(sharedProfileId);

        var canAccess = await _guard.CanAccessAsync(_accessibleMediaId, _userId);

        canAccess.Should().BeFalse();
    }

    [Test]
    public async Task CanAccessAsync_ShouldIgnorePersonalRestrictionProfile_WhenSharedProfileHasNoRestrictionAssigned()
    {
        var sharedProfileId = Guid.NewGuid();
        var personalRestrictedProfile = CreateTitleRestrictionProfile("Personal restriction", "Ok");
        _context.ContentRestrictionProfiles.Add(personalRestrictedProfile);
        personalRestrictedProfile.Users.Add(await _context.Users.SingleAsync(u => u.Id == _userId));
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Family",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        await _context.SaveChangesAsync();

        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns(sharedProfileId);

        var canAccess = await _guard.CanAccessAsync(_accessibleMediaId, _userId);

        canAccess.Should().BeTrue();
    }

    [Test]
    public async Task CanAccessAsync_ShouldAllowUnratedMovie_WhenAgeRestrictionIsDisabled()
    {
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.DateOfBirth = new DateOnly(2014, 9, 10);
        user.AgeRestrictionEnabled = false;
        await _context.SaveChangesAsync();

        var canAccess = await _guard.CanAccessAsync(_accessibleMediaId, _userId);

        canAccess.Should().BeTrue();
    }

    [Test]
    public async Task CanAccessAsync_ShouldDenyUnratedMovie_WhenAgeRestrictionIsEnabled()
    {
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.DateOfBirth = new DateOnly(2014, 9, 10);
        user.AgeRestrictionEnabled = true;
        await _context.SaveChangesAsync();

        var canAccess = await _guard.CanAccessAsync(_accessibleMediaId, _userId);

        canAccess.Should().BeFalse();
    }

    [Test]
    public async Task CanAccessAsync_ShouldAllowUnratedMovie_WhenHideUnratedTitlesIsOff()
    {
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.DateOfBirth = new DateOnly(2014, 9, 10);
        user.AgeRestrictionEnabled = true;
        user.HideUnratedTitles = false;
        await _context.SaveChangesAsync();

        var canAccess = await _guard.CanAccessAsync(_accessibleMediaId, _userId);

        canAccess.Should().BeTrue();
    }

    [Test]
    public async Task CanAccessAsync_ShouldDenyEpisode_WhenParentSerieRatingExceedsAge()
    {
        var episodeId = await SeedAdultSerieEpisodeAsync();
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.DateOfBirth = new DateOnly(2014, 9, 10);
        user.AgeRestrictionEnabled = true;
        await _context.SaveChangesAsync();

        var canAccess = await _guard.CanAccessAsync(episodeId, _userId);

        canAccess.Should().BeFalse();
    }

    private async Task<Guid> SeedAdultSerieEpisodeAsync()
    {
        var serieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var tag = new MetadataTag
        {
            Kind = MetadataTagKind.ContentRating,
            NormalizedKey = "tv-ma",
            DisplayName = "TV-MA"
        };
        var serie = new Serie { Id = serieId, Title = "Adult Show" };
        var season = new SerieSeason { Id = seasonId, Title = "S1", SerieId = serieId, SeasonNumber = 1 };
        var episode = new SerieEpisode
        {
            Id = episodeId,
            Title = "E1",
            SerieId = serieId,
            SeasonId = seasonId,
            EpisodeNumber = 1
        };

        _context.MetadataTags.Add(tag);
        _context.Medias.AddRange(serie, season, episode);
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = serieId, LibraryId = _libraryId },
            new MediaLibraryAvailability { MediaId = seasonId, LibraryId = _libraryId },
            new MediaLibraryAvailability { MediaId = episodeId, LibraryId = _libraryId });
        await _context.SaveChangesAsync();

        serie.MetadataTags.Add(new MediaMetadataTag { MediaId = serieId, MetadataTagId = tag.Id });
        await _context.SaveChangesAsync();

        return episodeId;
    }

    private static ContentRestrictionProfile CreateTitleRestrictionProfile(string name, string blockedTitle) =>
        new()
        {
            Name = name,
            RuleFilter = new RuleGroup
            {
                MatchCondition = RuleMatchCondition.Any,
                Items =
                [
                    new ConditionRuleItem
                    {
                        Field = nameof(DynamicPlaylistField.Title),
                        Operator = RuleOperator.Equals,
                        Value = blockedTitle
                    }
                ]
            }
        };
}
