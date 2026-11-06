using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.MusicRadio.Queries.GetMusicRadio;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.MusicRadio.Queries;

[TestFixture]
public class GetMusicRadioQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IMusicIntelligenceService _musicIntelligence = null!;
    private GetMusicRadioQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _libraryId;
    private Guid _albumId;
    private Guid _artistId;
    private Guid _favoriteTrackId;
    private Guid _similarTrackId;

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
        _libraryId = Guid.NewGuid();
        _albumId = Guid.NewGuid();
        _artistId = Guid.NewGuid();
        _favoriteTrackId = Guid.NewGuid();
        _similarTrackId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, IdentityUserId = "ident", DisplayName = "listener" });
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Music",
            MediaType = LibraryMediaType.Music
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Music,
            Title = "Music",
            MetadataProviderName = "none",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });

        var artist = new MusicArtist { Id = _artistId, Title = "Artist", SortTitle = "Artist" };
        var album = new MusicAlbum
        {
            Id = _albumId,
            Title = "Album",
            SortTitle = "Album",
            ArtistId = _artistId,
            Artist = artist
        };
        var favorite = CreateTrack(_favoriteTrackId, "Favorite", _artistId, album);
        var similar = CreateTrack(_similarTrackId, "Similar", _artistId, album);
        _context.Medias.AddRange(artist, album, favorite, similar);
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = _favoriteTrackId, LibraryId = _libraryId },
            new MediaLibraryAvailability { MediaId = _similarTrackId, LibraryId = _libraryId });
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _favoriteTrackId,
            Value = 10,
            MinimumValue = 0,
            MaximumValue = 10
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetIdAsync(Arg.Any<CancellationToken>()).Returns(_userId);
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);
        _musicIntelligence = Substitute.For<IMusicIntelligenceService>();
        _musicIntelligence.IsEnabledAsync(Arg.Any<CancellationToken>()).Returns(true);

        _handler = new GetMusicRadioQueryHandler(
            _context,
            _currentUser,
            _musicIntelligence,
            new LiteMediaProjectionService(_context),
            new MediaAccessFilter(_context));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_Artist_ShouldReturnTracksLinkedToMusicArtist()
    {
        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.Artist,
            SeedArtistId = _artistId,
            Limit = 10
        }, CancellationToken.None);

        result.Select(t => t.Id).Should().BeEquivalentTo([_favoriteTrackId, _similarTrackId]);
    }

    [Test]
    public async Task Handle_Artist_ShouldIncludeAlbumArtistAndGuestCredits()
    {
        var albumOnlyTrackId = Guid.NewGuid();
        var guestTrackId = Guid.NewGuid();
        var otherArtistId = Guid.NewGuid();
        var albumOnlyAlbumId = Guid.NewGuid();
        var guestAlbumId = Guid.NewGuid();

        var otherArtist = new MusicArtist { Id = otherArtistId, Title = "Other", SortTitle = "Other" };
        var albumOnly = new MusicAlbum
        {
            Id = albumOnlyAlbumId,
            Title = "Album Only",
            SortTitle = "Album Only",
            ArtistId = _artistId
        };
        var albumOnlyTrack = CreateTrack(albumOnlyTrackId, "Album Artist Track", null, albumOnly);
        var guestAlbum = new MusicAlbum
        {
            Id = guestAlbumId,
            Title = "Guest Album",
            SortTitle = "Guest Album",
            ArtistId = otherArtistId,
            Artist = otherArtist
        };
        var guestTrack = CreateTrack(guestTrackId, "Guest Track", otherArtistId, guestAlbum);
        guestTrack.ArtistCredits.Add(new MusicArtistCredit
        {
            Id = Guid.NewGuid(),
            MusicArtistId = _artistId,
            MediaId = guestTrackId,
            IsGuest = true
        });

        _context.Medias.AddRange(otherArtist, albumOnly, albumOnlyTrack, guestAlbum, guestTrack);
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = albumOnlyTrackId, LibraryId = _libraryId },
            new MediaLibraryAvailability { MediaId = guestTrackId, LibraryId = _libraryId });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.Artist,
            SeedArtistId = _artistId,
            Limit = 10
        }, CancellationToken.None);

        result.Select(t => t.Id).Should().BeEquivalentTo(
            [_favoriteTrackId, _similarTrackId, albumOnlyTrackId, guestTrackId]);
    }

    [Test]
    public async Task Handle_Artist_ShouldMixSameGenreTracksFromOtherArtists()
    {
        var otherArtistId = Guid.NewGuid();
        var otherAlbumId = Guid.NewGuid();
        var rockTrackId = Guid.NewGuid();
        var jazzTrackId = Guid.NewGuid();
        var otherArtist = new MusicArtist { Id = otherArtistId, Title = "Other", SortTitle = "Other" };
        var otherAlbum = new MusicAlbum
        {
            Id = otherAlbumId,
            Title = "Other Album",
            SortTitle = "Other Album",
            ArtistId = otherArtistId,
            Artist = otherArtist
        };
        var rockTrack = CreateTrack(rockTrackId, "Rock Neighbor", otherArtistId, otherAlbum);
        var jazzTrack = CreateTrack(jazzTrackId, "Jazz Neighbor", otherArtistId, otherAlbum);

        _context.Medias.AddRange(otherArtist, otherAlbum, rockTrack, jazzTrack);
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = rockTrackId, LibraryId = _libraryId },
            new MediaLibraryAvailability { MediaId = jazzTrackId, LibraryId = _libraryId });
        AddGenre(_favoriteTrackId, "Rock");
        AddGenre(rockTrackId, "Rock");
        AddGenre(jazzTrackId, "Jazz");
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.Artist,
            SeedArtistId = _artistId,
            Limit = 10
        }, CancellationToken.None);

        var ids = result.Select(t => t.Id).ToList();
        ids.Should().Contain(_favoriteTrackId);
        ids.Should().Contain(_similarTrackId);
        ids.Should().Contain(rockTrackId);
        ids.Should().NotContain(jazzTrackId);
    }

    [Test]
    public async Task Handle_Artist_ShouldInterleaveSimilarArtistTracks_WhenIntelligenceEnabled()
    {
        var neighbor = AddArtistCatalog("Neighbor", 4);
        var outsiderId = AddArtistCatalog("Outsider", 1).TrackIds[0];
        AddGenre(_favoriteTrackId, "Rock");
        AddGenre(outsiderId, "Rock");
        await _context.SaveChangesAsync();

        _musicIntelligence.IsEnabledAsync(Arg.Any<CancellationToken>()).Returns(true);
        _musicIntelligence
            .GetSimilarArtistsAsync(_artistId, Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new MusicSimilarArtistMatchDto { ArtistId = neighbor.ArtistId.ToString(), Artist = "Neighbor" }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.Artist,
            SeedArtistId = _artistId,
            Limit = 6
        }, CancellationToken.None);

        var ids = result.Select(t => t.Id).ToList();
        ids.Should().HaveCount(6);
        new[] { ids[0], ids[3] }.Should().BeEquivalentTo([_favoriteTrackId, _similarTrackId]);
        new[] { ids[1], ids[2], ids[4], ids[5] }.Should().BeEquivalentTo(neighbor.TrackIds);
        ids.Should().NotContain(outsiderId);
    }

    [Test]
    public async Task Handle_Artist_ShouldResolveSimilarArtistsByName_WhenIntelligenceIdIsExternal()
    {
        var neighbor = AddArtistCatalog("Neighbor", 1);
        await _context.SaveChangesAsync();

        _musicIntelligence.IsEnabledAsync(Arg.Any<CancellationToken>()).Returns(true);
        _musicIntelligence
            .GetSimilarArtistsAsync(_artistId, Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new MusicSimilarArtistMatchDto { ArtistId = "audiomuse-id", Artist = "Neighbor" }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.Artist,
            SeedArtistId = _artistId,
            Limit = 6
        }, CancellationToken.None);

        result.Select(t => t.Id).Should().Contain(neighbor.TrackIds[0]);
    }

    [Test]
    public async Task Handle_Artist_ShouldContinueWithSimilarTracks_WhenSeedArtistTracksAreExcluded()
    {
        var neighbor = AddArtistCatalog("Neighbor", 3);
        await _context.SaveChangesAsync();

        _musicIntelligence.IsEnabledAsync(Arg.Any<CancellationToken>()).Returns(true);
        _musicIntelligence
            .GetSimilarArtistsAsync(_artistId, Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new MusicSimilarArtistMatchDto { ArtistId = neighbor.ArtistId.ToString(), Artist = "Neighbor" }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.Artist,
            SeedArtistId = _artistId,
            ExcludeIds = [_favoriteTrackId, _similarTrackId],
            Limit = 3
        }, CancellationToken.None);

        result.Select(t => t.Id).Should().BeEquivalentTo(neighbor.TrackIds);
    }

    [Test]
    public async Task Handle_DiscoveryAi_ShouldUseFavoriteTrackAsSimilarSeed()
    {
        _musicIntelligence
            .GetSimilarTracksAsync(
                _favoriteTrackId,
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns([new MusicIntelligenceTrackMatchDto { ItemId = _similarTrackId }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.DiscoveryAi,
            Limit = 3
        }, CancellationToken.None);

        result.Should().ContainSingle(t => t.Id == _similarTrackId);
        await _musicIntelligence.DidNotReceive()
            .GetDiscoveryTracksAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_DiscoveryAi_ShouldExcludeRatedNeighbors_EvenWhenNeverPlayed()
    {
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _similarTrackId,
            Value = 4,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        _musicIntelligence
            .GetSimilarTracksAsync(
                _favoriteTrackId,
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns([new MusicIntelligenceTrackMatchDto { ItemId = _similarTrackId }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.DiscoveryAi,
            Limit = 3
        }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_DiscoveryAi_ShouldKeepPlayedUnratedNeighbors()
    {
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _similarTrackId,
            PlayCount = 4,
            LastInteractedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _musicIntelligence
            .GetSimilarTracksAsync(
                _favoriteTrackId,
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns([new MusicIntelligenceTrackMatchDto { ItemId = _similarTrackId }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.DiscoveryAi,
            Limit = 3
        }, CancellationToken.None);

        result.Should().ContainSingle(t => t.Id == _similarTrackId);
    }

    [Test]
    public async Task Handle_DiscoveryAi_ShouldReturnEmpty_WhenNeighborsWereAlreadyRated()
    {
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _similarTrackId,
            PlayCount = 4,
            LastInteractedAt = DateTime.UtcNow
        });
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _similarTrackId,
            Value = 4,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        _musicIntelligence
            .GetSimilarTracksAsync(
                _favoriteTrackId,
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns([new MusicIntelligenceTrackMatchDto { ItemId = _similarTrackId }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.DiscoveryAi,
            Limit = 3
        }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Discovery_ShouldExcludeAlreadyRatedTracks()
    {
        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.Discovery,
            Limit = 3
        }, CancellationToken.None);

        result.Should().ContainSingle(t => t.Id == _similarTrackId);
        result.Should().NotContain(t => t.Id == _favoriteTrackId);
    }

    [Test]
    public async Task Handle_Discovery_ShouldExcludeRatedTrack_EvenWhenNeverPlayed()
    {
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _similarTrackId,
            Value = 3,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.Discovery,
            Limit = 3
        }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Discovery_ShouldExcludeTracks_WhenAlbumIsRated()
    {
        _context.Ratings.RemoveRange(_context.Ratings.OfType<UserRating>().Where(r => r.MediaId == _favoriteTrackId));
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _albumId,
            Value = 8,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.Discovery,
            Limit = 3
        }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Discovery_ShouldExcludeTracks_WhenArtistIsRated()
    {
        _context.Ratings.RemoveRange(_context.Ratings.OfType<UserRating>().Where(r => r.MediaId == _favoriteTrackId));
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _artistId,
            Value = 9,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.Discovery,
            Limit = 3
        }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_DiscoveryAi_ShouldExcludeNeighbors_WhenAlbumIsRated()
    {
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _albumId,
            Value = 7,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        _musicIntelligence
            .GetSimilarTracksAsync(
                _favoriteTrackId,
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns([new MusicIntelligenceTrackMatchDto { ItemId = _similarTrackId }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.DiscoveryAi,
            Limit = 3
        }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_DiscoveryAi_ShouldIgnoreOtherUsersRatings_OnNeighbors()
    {
        var otherUserId = Guid.NewGuid();
        _context.Users.Add(new User { Id = otherUserId, IdentityUserId = "other", DisplayName = "other" });
        _context.Ratings.Add(new UserRating
        {
            UserId = otherUserId,
            MediaId = _similarTrackId,
            Value = 10,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        _musicIntelligence
            .GetSimilarTracksAsync(
                _favoriteTrackId,
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns([new MusicIntelligenceTrackMatchDto { ItemId = _similarTrackId }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.DiscoveryAi,
            Limit = 3
        }, CancellationToken.None);

        result.Should().ContainSingle(t => t.Id == _similarTrackId);
        var track = result.OfType<MusicTrack>().Single();
        track.Ratings.OfType<UserRating>().Should().BeEmpty();
    }

    [Test]
    public async Task Handle_RecentlyAdded_ShouldExcludeTracks_WhenLibraryIsExcluded()
    {
        var hiddenTrackId = AddHiddenLibraryTrack();
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.RecentlyAdded,
            Limit = 50
        }, CancellationToken.None);

        result.Select(t => t.Id).Should().NotContain(hiddenTrackId);
        result.Select(t => t.Id).Should().Contain(_favoriteTrackId);
    }

    [Test]
    public async Task Handle_Sonic_ShouldDropTracks_WhenLibraryIsExcluded()
    {
        var hiddenTrackId = AddHiddenLibraryTrack();
        await _context.SaveChangesAsync();

        _musicIntelligence.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        _musicIntelligence
            .GetSimilarTracksAsync(
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns([new MusicIntelligenceTrackMatchDto { ItemId = hiddenTrackId }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.Sonic,
            Limit = 10
        }, CancellationToken.None);

        result.Select(t => t.Id).Should().NotContain(hiddenTrackId);
    }

    [Test]
    public async Task Handle_DiscoveryAi_ShouldReturnEmpty_WhenIntelligenceDisabled()
    {
        _musicIntelligence.IsEnabledAsync(Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.DiscoveryAi,
            Limit = 3
        }, CancellationToken.None);

        result.Should().BeEmpty();
        await _musicIntelligence.DidNotReceive()
            .GetSimilarTracksAsync(
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    private Guid AddHiddenLibraryTrack()
    {
        var hiddenLibraryId = Guid.NewGuid();
        var hiddenTrackId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var albumId = Guid.NewGuid();

        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Hidden",
            MediaType = LibraryMediaType.Music
        });
        _context.Libraries.Add(new Library
        {
            Id = hiddenLibraryId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Music,
            Title = "Hidden",
            MetadataProviderName = "none",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });

        var album = new MusicAlbum
        {
            Id = albumId,
            Title = "Hidden Album",
            SortTitle = "Hidden Album",
            ArtistId = _artistId
        };
        var track = CreateTrack(hiddenTrackId, "PRIVATE-MARKER", _artistId, album);
        _context.Medias.AddRange(album, track);
        _context.MediaLibraryAvailabilities.Add(
            new MediaLibraryAvailability { MediaId = hiddenTrackId, LibraryId = hiddenLibraryId });
        _context.UserLibraryExclusions.Add(new UserLibraryExclusion
        {
            UserId = _userId,
            LibraryId = hiddenLibraryId,
            IsAdminExcluded = true
        });

        return hiddenTrackId;
    }

    private (Guid ArtistId, Guid[] TrackIds) AddArtistCatalog(string name, int trackCount)
    {
        var artistId = Guid.NewGuid();
        var albumId = Guid.NewGuid();
        var artist = new MusicArtist { Id = artistId, Title = name, SortTitle = name };
        var album = new MusicAlbum
        {
            Id = albumId,
            Title = name,
            SortTitle = name,
            ArtistId = artistId,
            Artist = artist
        };
        var trackIds = new Guid[trackCount];
        var tracks = new MusicTrack[trackCount];
        for (var i = 0; i < trackCount; i++)
        {
            trackIds[i] = Guid.NewGuid();
            tracks[i] = CreateTrack(trackIds[i], $"{name} {i}", artistId, album);
        }

        _context.Medias.AddRange(tracks);
        _context.Medias.AddRange(artist, album);
        _context.MediaLibraryAvailabilities.AddRange(trackIds.Select(id =>
            new MediaLibraryAvailability { MediaId = id, LibraryId = _libraryId }));
        return (artistId, trackIds);
    }

    private void AddGenre(Guid mediaId, string name)
    {
        var key = name.ToLowerInvariant();
        var tag = _context.MetadataTags.Local.FirstOrDefault(t =>
            t.Kind == MetadataTagKind.Genre && t.NormalizedKey == key);
        if (tag is null)
        {
            tag = new MetadataTag
            {
                Id = Guid.NewGuid(),
                Kind = MetadataTagKind.Genre,
                NormalizedKey = key,
                DisplayName = name
            };
            _context.MetadataTags.Add(tag);
        }

        _context.MediaMetadataTags.Add(new MediaMetadataTag
        {
            MediaId = mediaId,
            MetadataTagId = tag.Id
        });
    }

    private static MusicTrack CreateTrack(Guid trackId, string title, Guid? artistId, MusicAlbum album) =>
        new()
        {
            Id = trackId,
            Title = title,
            SortTitle = title,
            ArtistId = artistId,
            Artist = album.Artist,
            AlbumId = album.Id,
            Album = album
        };
}
