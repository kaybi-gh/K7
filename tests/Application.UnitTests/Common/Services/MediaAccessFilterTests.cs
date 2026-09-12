using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Common.Services;

[TestFixture]
public class MediaAccessFilterTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private MediaAccessFilter _filter = null!;

    private Guid _userId;
    private Guid _visibleMediaId;
    private Guid _excludedMediaId;
    private Guid _libraryExcludedMediaId;
    private Guid _visibleLibraryId;
    private Guid _excludedLibraryId;

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
        _filter = new MediaAccessFilter(_context);

        _userId = Guid.NewGuid();
        _visibleMediaId = Guid.NewGuid();
        _excludedMediaId = Guid.NewGuid();
        _libraryExcludedMediaId = Guid.NewGuid();
        _visibleLibraryId = Guid.NewGuid();
        _excludedLibraryId = Guid.NewGuid();

        var groupId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, DisplayName = "viewer" });
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.AddRange(
            new Library
            {
                Id = _visibleLibraryId,
                LibraryGroupId = groupId,
                MediaType = LibraryMediaType.Movie,
                Title = "Visible",
                MetadataProviderName = "tmdb",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en"
            },
            new Library
            {
                Id = _excludedLibraryId,
                LibraryGroupId = groupId,
                MediaType = LibraryMediaType.Movie,
                Title = "Excluded",
                MetadataProviderName = "tmdb",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en"
            });

        _context.Medias.AddRange(
            new Movie { Id = _visibleMediaId, Title = "Visible" },
            new Movie { Id = _excludedMediaId, Title = "Excluded media" },
            new Movie { Id = _libraryExcludedMediaId, Title = "Library excluded" });

        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = _visibleMediaId, LibraryId = _visibleLibraryId },
            new MediaLibraryAvailability { MediaId = _excludedMediaId, LibraryId = _visibleLibraryId },
            new MediaLibraryAvailability { MediaId = _libraryExcludedMediaId, LibraryId = _excludedLibraryId });

        _context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            UserId = _userId,
            MediaId = _excludedMediaId,
            IsSelfExcluded = true
        });
        _context.UserLibraryExclusions.Add(new UserLibraryExclusion
        {
            UserId = _userId,
            LibraryId = _excludedLibraryId,
            IsAdminExcluded = true
        });

        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ApplyExclusions_ShouldHideUserExcludedMediaAndExcludedLibraries()
    {
        var results = await _filter
            .ApplyExclusions(_context.Medias, _userId)
            .Select(m => m.Id)
            .ToListAsync();

        results.Should().ContainSingle().Which.Should().Be(_visibleMediaId);
    }

    [Test]
    public async Task GetAccessibleMediaIds_ShouldMatchApplyExclusions()
    {
        var ids = await _filter.GetAccessibleMediaIds(_userId).ToListAsync();

        ids.Should().ContainSingle().Which.Should().Be(_visibleMediaId);
    }

    [Test]
    public async Task ApplyUnavailablePeerExclusion_ShouldHideMediaFromDownPeers()
    {
        var activePeerId = Guid.NewGuid();
        var downPeerId = Guid.NewGuid();
        var activeRemoteMediaId = Guid.NewGuid();
        var downRemoteMediaId = Guid.NewGuid();

        var activePeer = PeerServer.CreateActiveInbound(
            "active-peer",
            "https://active.example",
            Guid.NewGuid().ToString("N"),
            autoAddNewLibraries: true,
            federationAssertionSecret: Guid.NewGuid().ToString("N"));
        activePeer.Id = activePeerId;
        activePeer.LastTestSucceeded = true;

        var downPeer = PeerServer.CreateActiveInbound(
            "down-peer",
            "https://down.example",
            Guid.NewGuid().ToString("N"),
            autoAddNewLibraries: true,
            federationAssertionSecret: Guid.NewGuid().ToString("N"));
        downPeer.Id = downPeerId;
        downPeer.LastTestSucceeded = false;

        _context.PeerServers.AddRange(activePeer, downPeer);
        _context.Medias.AddRange(
            new Movie { Id = activeRemoteMediaId, Title = "Active remote", PeerServerId = activePeerId },
            new Movie { Id = downRemoteMediaId, Title = "Down remote", PeerServerId = downPeerId });
        await _context.SaveChangesAsync();

        var results = await _filter
            .ApplyUnavailablePeerExclusion(_context.Medias)
            .Select(m => m.Id)
            .ToListAsync();

        results.Should().Contain(_visibleMediaId);
        results.Should().Contain(activeRemoteMediaId);
        results.Should().NotContain(downRemoteMediaId);
        results.Should().Contain(_excludedMediaId);
    }

    [Test]
    public async Task GetRestrictionProfileAsync_ShouldReturnPersonalProfile_WhenNoSharedProfile()
    {
        var profile = new ContentRestrictionProfile { Name = "Personal" };
        _context.ContentRestrictionProfiles.Add(profile);
        profile.Users.Add(await _context.Users.SingleAsync(u => u.Id == _userId));
        await _context.SaveChangesAsync();

        var result = await _filter.GetRestrictionProfileAsync(_userId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Personal");
    }

    [Test]
    public async Task GetRestrictionProfileAsync_ShouldReturnSharedProfileRestriction_WhenSharedSessionIsActive()
    {
        var sharedProfileId = Guid.NewGuid();
        var personal = new ContentRestrictionProfile { Name = "Personal" };
        var shared = new ContentRestrictionProfile { Name = "Shared" };
        _context.ContentRestrictionProfiles.AddRange(personal, shared);
        personal.Users.Add(await _context.Users.SingleAsync(u => u.Id == _userId));
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Kids",
            HostUserId = _userId,
            CreatedByUserId = _userId,
            ContentRestrictionProfile = shared
        });
        await _context.SaveChangesAsync();

        var result = await _filter.GetRestrictionProfileAsync(_userId, sharedProfileId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Shared");
    }

    [Test]
    public async Task GetRestrictionProfileAsync_ShouldReturnNull_WhenSharedProfileHasNoRestriction()
    {
        var sharedProfileId = Guid.NewGuid();
        var personal = new ContentRestrictionProfile { Name = "Personal" };
        _context.ContentRestrictionProfiles.Add(personal);
        personal.Users.Add(await _context.Users.SingleAsync(u => u.Id == _userId));
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Adults",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        await _context.SaveChangesAsync();

        var result = await _filter.GetRestrictionProfileAsync(_userId, sharedProfileId);

        result.Should().BeNull();
    }

    [Test]
    public async Task ApplyAllAsync_ShouldNotFilterByAge_WhenDateOfBirthIsSetButRestrictionIsDisabled()
    {
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.DateOfBirth = new DateOnly(2018, 1, 1);
        user.AgeRestrictionEnabled = false;
        await _context.SaveChangesAsync();

        var ids = await (await _filter.ApplyAllAsync(_context.Medias, _userId))
            .Select(m => m.Id)
            .ToListAsync();

        ids.Should().Contain(_visibleMediaId);
    }

    [Test]
    public async Task ApplyAllAsync_ShouldHideUnratedMovie_WhenAgeRestrictionIsEnabled()
    {
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.DateOfBirth = new DateOnly(2014, 9, 10);
        user.AgeRestrictionEnabled = true;
        await _context.SaveChangesAsync();

        var ids = await (await _filter.ApplyAllAsync(_context.Medias, _userId))
            .Select(m => m.Id)
            .ToListAsync();

        ids.Should().NotContain(_visibleMediaId);
    }

    [Test]
    public async Task ApplyAllAsync_ShouldKeepUnratedMovie_WhenHideUnratedTitlesIsOff()
    {
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.DateOfBirth = new DateOnly(2014, 9, 10);
        user.AgeRestrictionEnabled = true;
        user.HideUnratedTitles = false;
        await _context.SaveChangesAsync();

        var ids = await (await _filter.ApplyAllAsync(_context.Medias, _userId))
            .Select(m => m.Id)
            .ToListAsync();

        ids.Should().Contain(_visibleMediaId);
    }

    [Test]
    public async Task ApplyAllAsync_ShouldHideSerieSeasonAndEpisode_WhenParentRatingExceedsAge()
    {
        var (serieId, seasonId, episodeId, kidsSerieId) = await SeedRestrictedSerieGraphAsync();
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.DateOfBirth = new DateOnly(2014, 9, 10);
        user.AgeRestrictionEnabled = true;
        await _context.SaveChangesAsync();

        var ids = await (await _filter.ApplyAllAsync(_context.Medias, _userId))
            .Select(m => m.Id)
            .ToListAsync();

        ids.Should().Contain(kidsSerieId);
        ids.Should().NotContain(serieId);
        ids.Should().NotContain(seasonId);
        ids.Should().NotContain(episodeId);
    }

    [Test]
    public async Task ApplyAllAsync_ShouldNotFilterByAge_WhenEnabledWithoutDateOfBirth()
    {
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.AgeRestrictionEnabled = true;
        user.DateOfBirth = null;
        await _context.SaveChangesAsync();

        var ids = await (await _filter.ApplyAllAsync(_context.Medias, _userId))
            .Select(m => m.Id)
            .ToListAsync();

        ids.Should().Contain(_visibleMediaId);
    }

    [Test]
    public async Task ApplyAllAsync_ShouldUseSharedProfileAgeGate_InsteadOfUserGate()
    {
        var sharedProfileId = Guid.NewGuid();
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.DateOfBirth = new DateOnly(2018, 1, 1);
        user.AgeRestrictionEnabled = true;
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Adults",
            HostUserId = _userId,
            CreatedByUserId = _userId,
            AgeRestrictionEnabled = false
        });
        await _context.SaveChangesAsync();

        var ids = await (await _filter.ApplyAllAsync(_context.Medias, _userId, sharedProfileId, CancellationToken.None))
            .Select(m => m.Id)
            .ToListAsync();

        ids.Should().Contain(_visibleMediaId);
    }

    [Test]
    public async Task ApplyAllAsync_ShouldApplySharedProfileAgeGate_WhenUserGateIsOff()
    {
        var sharedProfileId = Guid.NewGuid();
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Kids",
            HostUserId = _userId,
            CreatedByUserId = _userId,
            AgeRestrictionEnabled = true,
            ViewerDateOfBirth = new DateOnly(2018, 1, 1)
        });
        await _context.SaveChangesAsync();

        var ids = await (await _filter.ApplyAllAsync(_context.Medias, _userId, sharedProfileId, CancellationToken.None))
            .Select(m => m.Id)
            .ToListAsync();

        ids.Should().NotContain(_visibleMediaId);
    }

    [Test]
    public async Task ApplyAllAsync_ShouldStackAgeGateWithRestrictionProfile()
    {
        var allowedId = Guid.NewGuid();
        var blockedByProfileId = Guid.NewGuid();
        var gTag = new MetadataTag
        {
            Kind = MetadataTagKind.ContentRating,
            NormalizedKey = "g",
            DisplayName = "G"
        };
        _context.MetadataTags.Add(gTag);
        _context.Medias.AddRange(
            new Movie { Id = allowedId, Title = "Kids Movie" },
            new Movie { Id = blockedByProfileId, Title = "Forbidden" });
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = allowedId, LibraryId = _visibleLibraryId },
            new MediaLibraryAvailability { MediaId = blockedByProfileId, LibraryId = _visibleLibraryId });
        await _context.SaveChangesAsync();

        var allowedMovie = await _context.Medias.SingleAsync(m => m.Id == allowedId);
        var blockedMovie = await _context.Medias.SingleAsync(m => m.Id == blockedByProfileId);
        allowedMovie.MetadataTags.Add(new MediaMetadataTag { MediaId = allowedId, MetadataTagId = gTag.Id });
        blockedMovie.MetadataTags.Add(new MediaMetadataTag { MediaId = blockedByProfileId, MetadataTagId = gTag.Id });

        var profile = new ContentRestrictionProfile
        {
            Name = "Block Forbidden",
            RuleFilter = new RuleGroup
            {
                MatchCondition = RuleMatchCondition.Any,
                Items =
                [
                    new ConditionRuleItem
                    {
                        Field = nameof(DynamicPlaylistField.Title),
                        Operator = RuleOperator.Equals,
                        Value = "Forbidden"
                    }
                ]
            }
        };
        _context.ContentRestrictionProfiles.Add(profile);
        profile.Users.Add(await _context.Users.SingleAsync(u => u.Id == _userId));

        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.DateOfBirth = new DateOnly(2018, 1, 1);
        user.AgeRestrictionEnabled = true;
        await _context.SaveChangesAsync();

        var ids = await (await _filter.ApplyAllAsync(_context.Medias, _userId))
            .Select(m => m.Id)
            .ToListAsync();

        ids.Should().Contain(allowedId);
        ids.Should().NotContain(blockedByProfileId);
        ids.Should().NotContain(_visibleMediaId);
    }

    [Test]
    public async Task GetContentGatesAsync_ShouldReturnNulls_WhenAgeFlagIsOff()
    {
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.DateOfBirth = new DateOnly(2018, 1, 1);
        user.AgeRestrictionEnabled = false;
        await _context.SaveChangesAsync();

        var gates = await _filter.GetContentGatesAsync(_userId, sharedProfileId: null);

        gates.RestrictionProfile.Should().BeNull();
        gates.AgeGate.Should().BeNull();
    }

    private async Task<(Guid AdultSerieId, Guid SeasonId, Guid EpisodeId, Guid KidsSerieId)> SeedRestrictedSerieGraphAsync()
    {
        var adultSerieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var kidsSerieId = Guid.NewGuid();

        var adultTag = new MetadataTag
        {
            Kind = MetadataTagKind.ContentRating,
            NormalizedKey = "tv-ma",
            DisplayName = "TV-MA"
        };
        var kidsTag = new MetadataTag
        {
            Kind = MetadataTagKind.ContentRating,
            NormalizedKey = "tv-y",
            DisplayName = "TV-Y"
        };

        var adultSerie = new Serie { Id = adultSerieId, Title = "Adult Show" };
        var season = new SerieSeason { Id = seasonId, Title = "S1", SerieId = adultSerieId, SeasonNumber = 1 };
        var episode = new SerieEpisode
        {
            Id = episodeId,
            Title = "E1",
            SerieId = adultSerieId,
            SeasonId = seasonId,
            EpisodeNumber = 1
        };
        var kidsSerie = new Serie { Id = kidsSerieId, Title = "Kids Show" };

        _context.MetadataTags.AddRange(adultTag, kidsTag);
        _context.Medias.AddRange(adultSerie, season, episode, kidsSerie);
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = adultSerieId, LibraryId = _visibleLibraryId },
            new MediaLibraryAvailability { MediaId = seasonId, LibraryId = _visibleLibraryId },
            new MediaLibraryAvailability { MediaId = episodeId, LibraryId = _visibleLibraryId },
            new MediaLibraryAvailability { MediaId = kidsSerieId, LibraryId = _visibleLibraryId });
        await _context.SaveChangesAsync();

        adultSerie.MetadataTags.Add(new MediaMetadataTag { MediaId = adultSerieId, MetadataTagId = adultTag.Id });
        kidsSerie.MetadataTags.Add(new MediaMetadataTag { MediaId = kidsSerieId, MetadataTagId = kidsTag.Id });
        await _context.SaveChangesAsync();

        return (adultSerieId, seasonId, episodeId, kidsSerieId);
    }
}
