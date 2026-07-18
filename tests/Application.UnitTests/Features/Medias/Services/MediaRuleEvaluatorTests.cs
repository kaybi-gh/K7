using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

[TestFixture]
public class MediaRuleEvaluatorTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private Guid _userA;
    private Guid _userB;
    private Movie _inception = null!;
    private Movie _forrestGump = null!;
    private Movie _titanic = null!;
    private SerieEpisode _breakingBadPilot = null!;

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

        _userA = Guid.NewGuid();
        _userB = Guid.NewGuid();
        SeedData();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ApplyFilter_ActorNameContains_ShouldReturnMatchingMovies()
    {
        var filter = Rule(nameof(SmartPlaylistField.ActorName), RuleOperator.Contains, "DiCaprio");

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().BeEquivalentTo([_inception.Id, _titanic.Id]);
    }

    [Test]
    public async Task ApplyFilter_ActorNameContains_ShouldBeCaseInsensitive()
    {
        var filter = Rule(nameof(SmartPlaylistField.ActorName), RuleOperator.Contains, "dicaprio");

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().BeEquivalentTo([_inception.Id, _titanic.Id]);
    }

    [Test]
    public async Task ApplyFilter_ActorNameEquals_ShouldMatchExactName()
    {
        var filter = Rule(nameof(SmartPlaylistField.ActorName), RuleOperator.Equals, "Tom Hanks");

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().BeEquivalentTo([_forrestGump.Id]);
    }

    [Test]
    public async Task ApplyFilter_ActorNameOnEpisode_ShouldMatchSerieCast()
    {
        var filter = Rule(nameof(SmartPlaylistField.ActorName), RuleOperator.Contains, "Cranston");

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().Contain(_breakingBadPilot.Id);
        ids.Should().NotContain(_inception.Id);
        ids.Should().NotContain(_forrestGump.Id);
        ids.Should().NotContain(_titanic.Id);
    }

    [Test]
    public async Task ApplyFilter_ArtistNameContains_ShouldMatchAlbumAndArtistEntities()
    {
        var now = DateTimeOffset.UtcNow;
        var radiohead = new MusicArtist
        {
            Id = Guid.NewGuid(),
            Title = "Radiohead",
            Created = now,
            LastModified = now
        };
        var okComputer = new MusicAlbum
        {
            Id = Guid.NewGuid(),
            Title = "OK Computer",
            ArtistId = radiohead.Id,
            Artist = radiohead,
            Created = now,
            LastModified = now
        };
        radiohead.Albums.Add(okComputer);

        var coldplay = new MusicArtist
        {
            Id = Guid.NewGuid(),
            Title = "Coldplay",
            Created = now,
            LastModified = now
        };
        var parachutes = new MusicAlbum
        {
            Id = Guid.NewGuid(),
            Title = "Parachutes",
            ArtistId = coldplay.Id,
            Artist = coldplay,
            Created = now,
            LastModified = now
        };
        coldplay.Albums.Add(parachutes);

        _context.Medias.AddRange(radiohead, okComputer, coldplay, parachutes);
        await _context.SaveChangesAsync();

        var filter = Rule(nameof(SmartPlaylistField.ArtistName), RuleOperator.Contains, "Radio");
        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().BeEquivalentTo([radiohead.Id, okComputer.Id]);
    }

    [Test]
    public async Task ApplyFilter_ArtistNameContains_ShouldMatchTrackViaAlbumArtist()
    {
        var now = DateTimeOffset.UtcNow;
        var artist = new MusicArtist
        {
            Id = Guid.NewGuid(),
            Title = "Daft Punk",
            Created = now,
            LastModified = now
        };
        var album = new MusicAlbum
        {
            Id = Guid.NewGuid(),
            Title = "Discovery",
            ArtistId = artist.Id,
            Artist = artist,
            Created = now,
            LastModified = now
        };
        var track = new MusicTrack
        {
            Id = Guid.NewGuid(),
            Title = "One More Time",
            AlbumId = album.Id,
            Album = album,
            Created = now,
            LastModified = now
        };
        album.Tracks.Add(track);

        _context.Medias.AddRange(artist, album, track);
        await _context.SaveChangesAsync();

        var filter = Rule(nameof(SmartPlaylistField.ArtistName), RuleOperator.Contains, "Daft");
        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().Contain(track.Id);
    }

    [Test]
    public async Task ApplyFilter_OriginalLanguageEquals_ShouldMatchMovies()
    {
        var filter = Rule(nameof(SmartPlaylistField.OriginalLanguage), RuleOperator.Equals, "en");

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().BeEquivalentTo([_inception.Id, _forrestGump.Id, _titanic.Id]);
    }

    [Test]
    public async Task ApplyFilter_RatingGreaterThanOrEqual_ShouldScopeToCurrentUser()
    {
        var filter = Rule(nameof(SmartPlaylistField.Rating), RuleOperator.GreaterThanOrEqual, "8");

        var idsForUserA = await ApplyAndGetIdsAsync(filter, userId: _userA);
        var idsForUserB = await ApplyAndGetIdsAsync(filter, userId: _userB);

        idsForUserA.Should().BeEquivalentTo([_inception.Id]);
        idsForUserB.Should().BeEmpty();
    }

    [Test]
    public async Task ApplyFilter_IsCompletedTrue_ShouldScopeToCurrentUser()
    {
        var filter = Rule(nameof(SmartPlaylistField.IsCompleted), RuleOperator.Equals, "true");

        var idsForUserA = await ApplyAndGetIdsAsync(filter, userId: _userA);
        var idsForUserB = await ApplyAndGetIdsAsync(filter, userId: _userB);

        idsForUserA.Should().BeEquivalentTo([_inception.Id]);
        idsForUserB.Should().BeEmpty();
    }

    [Test]
    public async Task ApplyFilter_CombinedAndRules_ShouldRequireAllConditions()
    {
        var filter = new RuleGroup
        {
            MatchCondition = RuleMatchCondition.All,
            Items =
            [
                new ConditionRuleItem
                {
                    Field = nameof(SmartPlaylistField.ActorName),
                    Operator = RuleOperator.Contains,
                    Value = "DiCaprio"
                },
                new ConditionRuleItem
                {
                    Field = nameof(SmartPlaylistField.OriginalLanguage),
                    Operator = RuleOperator.Equals,
                    Value = "en"
                }
            ]
        };

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().BeEquivalentTo([_inception.Id, _titanic.Id]);
    }

    [Test]
    public async Task ApplyFilter_NestedAnyGroup_ShouldMatchEitherBranch()
    {
        var filter = new RuleGroup
        {
            MatchCondition = RuleMatchCondition.All,
            Items =
            [
                new NestedGroupItem
                {
                    MatchCondition = RuleMatchCondition.Any,
                    Items =
                    [
                        new ConditionRuleItem
                        {
                            Field = nameof(SmartPlaylistField.ActorName),
                            Operator = RuleOperator.Equals,
                            Value = "Tom Hanks"
                        },
                        new ConditionRuleItem
                        {
                            Field = nameof(SmartPlaylistField.Title),
                            Operator = RuleOperator.Equals,
                            Value = "Inception"
                        }
                    ]
                }
            ]
        };

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().BeEquivalentTo([_inception.Id, _forrestGump.Id]);
    }

    [Test]
    public void ApplyFilter_GeneratedSql_ShouldNotUseExpressionInvoke()
    {
        var filter = new RuleGroup
        {
            MatchCondition = RuleMatchCondition.All,
            Items =
            [
                Rule(nameof(SmartPlaylistField.ActorName), RuleOperator.Contains, "DiCaprio"),
                Rule(nameof(SmartPlaylistField.Rating), RuleOperator.GreaterThanOrEqual, "8")
            ]
        };

        var sql = MediaRuleEvaluator
            .ApplyFilter(_context.Medias.AsNoTracking(), filter, _userA)
            .ToQueryString();

        sql.Should().NotContain("Invoke");
        sql.Should().Contain("PersonRoles");
    }

    [Test]
    public async Task ApplyFilter_VoiceActor_ShouldBeIncludedInCastSearch()
    {
        var animated = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Animated Feature",
            OriginalLanguage = "en"
        };
        var voiceActor = new Person { Id = Guid.NewGuid(), Name = "Voice Star" };
        var role = new VoiceActor
        {
            Id = Guid.NewGuid(),
            PersonId = voiceActor.Id,
            Person = voiceActor,
            MediaId = animated.Id,
            Media = animated,
            CharacterName = "Hero"
        };
        animated.PersonRoles.Add(role);
        voiceActor.Roles.Add(role);
        _context.Medias.Add(animated);
        _context.Persons.Add(voiceActor);
        await _context.SaveChangesAsync();

        var filter = Rule(nameof(SmartPlaylistField.ActorName), RuleOperator.Contains, "Voice");
        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().Contain(animated.Id);
    }

    [Test]
    public async Task ApplyFilter_EmptyGroup_ShouldReturnAllMedia()
    {
        var filter = new RuleGroup { MatchCondition = RuleMatchCondition.All, Items = [] };

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().Contain([_inception.Id, _forrestGump.Id, _titanic.Id, _breakingBadPilot.Id]);
    }

    [Test]
    public async Task ApplyFilter_EmptyNestedGroup_ShouldActAsAlwaysTrueBranch()
    {
        var filter = new RuleGroup
        {
            MatchCondition = RuleMatchCondition.All,
            Items =
            [
                new NestedGroupItem { MatchCondition = RuleMatchCondition.Any, Items = [] }
            ]
        };

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().Contain([_inception.Id, _forrestGump.Id, _titanic.Id]);
    }

    [Test]
    public async Task ApplyFilter_DateAddedInLast_ShouldOnlyMatchRecentlyAddedMedia()
    {
        var now = DateTimeOffset.UtcNow;
        var recentMovie = CreateMovie("Recently Added Movie", "en");
        recentMovie.Created = now;
        var oldMovie = CreateMovie("Long Ago Movie", "en");
        oldMovie.Created = now.AddDays(-90);
        _context.Medias.AddRange(recentMovie, oldMovie);
        await _context.SaveChangesAsync();

        var filter = Rule(nameof(SmartPlaylistField.DateAdded), RuleOperator.InLast, "30");
        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().Contain(recentMovie.Id);
        ids.Should().NotContain(oldMovie.Id);
    }

    [Test]
    public async Task ApplyFilter_DateAddedInLast_WithNonNumericValue_ShouldFallBackToMatchingEverything()
    {
        var filter = Rule(nameof(SmartPlaylistField.DateAdded), RuleOperator.InLast, "not-a-number");

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().Contain([_inception.Id, _forrestGump.Id, _titanic.Id]);
    }

    [Test]
    public async Task ApplyFilter_LastPlayedInLast_ShouldScopeToUserAndRecentInteraction()
    {
        var now = DateTime.UtcNow;
        var recentlyPlayed = CreateMovie("Recently Played Movie", "en");
        var notPlayedRecently = CreateMovie("Movie Played Long Ago", "en");
        _context.Medias.AddRange(recentlyPlayed, notPlayedRecently);
        _context.UserMediaStates.AddRange(
            new UserMediaState
            {
                Id = Guid.NewGuid(),
                UserId = _userA,
                MediaId = recentlyPlayed.Id,
                Media = recentlyPlayed,
                LastInteractedAt = now.AddDays(-1),
                Created = DateTimeOffset.UtcNow,
                LastModified = DateTimeOffset.UtcNow
            },
            new UserMediaState
            {
                Id = Guid.NewGuid(),
                UserId = _userA,
                MediaId = notPlayedRecently.Id,
                Media = notPlayedRecently,
                LastInteractedAt = now.AddDays(-60),
                Created = DateTimeOffset.UtcNow,
                LastModified = DateTimeOffset.UtcNow
            });
        await _context.SaveChangesAsync();

        var filter = Rule(nameof(SmartPlaylistField.LastPlayed), RuleOperator.InLast, "7");
        var idsForUserA = await ApplyAndGetIdsAsync(filter, userId: _userA);
        var idsForUserB = await ApplyAndGetIdsAsync(filter, userId: _userB);

        idsForUserA.Should().Contain(recentlyPlayed.Id);
        idsForUserA.Should().NotContain(notPlayedRecently.Id);
        idsForUserB.Should().NotContain(recentlyPlayed.Id);
    }

    [Test]
    public async Task ApplyFilter_LastPlayedIsEmpty_ShouldExcludeMediaWithRecordedInteraction()
    {
        var now = DateTime.UtcNow;
        var interacted = CreateMovie("Interacted Movie", "en");
        var neverInteracted = CreateMovie("Never Interacted Movie", "en");
        _context.Medias.AddRange(interacted, neverInteracted);
        _context.UserMediaStates.Add(new UserMediaState
        {
            Id = Guid.NewGuid(),
            UserId = _userA,
            MediaId = interacted.Id,
            Media = interacted,
            LastInteractedAt = now,
            Created = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        var filter = Rule(nameof(SmartPlaylistField.LastPlayed), RuleOperator.IsEmpty, null);
        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().Contain(neverInteracted.Id);
        ids.Should().NotContain(interacted.Id);
    }

    [Test]
    public async Task ApplyFilter_YearIsEmpty_ShouldMatchMediaWithoutReleaseDate()
    {
        var filter = Rule(nameof(SmartPlaylistField.Year), RuleOperator.IsEmpty, null);

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().Contain([_inception.Id, _forrestGump.Id, _titanic.Id]);
    }

    [Test]
    public async Task ApplyFilter_UnknownField_ShouldMatchEverything()
    {
        var filter = Rule("SomeUnsupportedField", RuleOperator.Equals, "value");

        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().Contain([_inception.Id, _forrestGump.Id, _titanic.Id]);
    }

    [Test]
    public async Task ApplyFilter_CrewMember_ShouldNotMatchActorNameFilter()
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Directed Movie",
            OriginalLanguage = "en"
        };
        var director = new Person { Id = Guid.NewGuid(), Name = "Famous Director" };
        var role = new CrewMember
        {
            Id = Guid.NewGuid(),
            PersonId = director.Id,
            Person = director,
            MediaId = movie.Id,
            Media = movie,
            Job = "Director",
            Department = "Directing"
        };
        movie.PersonRoles.Add(role);
        director.Roles.Add(role);
        _context.Medias.Add(movie);
        _context.Persons.Add(director);
        await _context.SaveChangesAsync();

        var filter = Rule(nameof(SmartPlaylistField.ActorName), RuleOperator.Contains, "Director");
        var ids = await ApplyAndGetIdsAsync(filter, userId: _userA);

        ids.Should().NotContain(movie.Id);
    }

    private async Task<List<Guid>> ApplyAndGetIdsAsync(ConditionRuleItem rule, Guid? userId) =>
        await ApplyAndGetIdsAsync(SingleRuleGroup(rule), userId);

    private async Task<List<Guid>> ApplyAndGetIdsAsync(RuleGroup filter, Guid? userId) =>
        await MediaRuleEvaluator
            .ApplyFilter(_context.Medias.AsNoTracking(), filter, userId)
            .Select(m => m.Id)
            .ToListAsync();

    private static RuleGroup SingleRuleGroup(ConditionRuleItem rule) => new()
    {
        MatchCondition = RuleMatchCondition.All,
        Items = [rule]
    };

    private static ConditionRuleItem Rule(string field, RuleOperator op, string? value) => new()
    {
        Field = field,
        Operator = op,
        Value = value
    };

    private void SeedData()
    {
        var now = DateTimeOffset.UtcNow;
        var userA = new User { Id = _userA, Created = now, LastModified = now };
        var userB = new User { Id = _userB, Created = now, LastModified = now };

        var diCaprio = new Person { Id = Guid.NewGuid(), Name = "Leonardo DiCaprio", Created = now, LastModified = now };
        var hanks = new Person { Id = Guid.NewGuid(), Name = "Tom Hanks", Created = now, LastModified = now };
        var cranston = new Person { Id = Guid.NewGuid(), Name = "Bryan Cranston", Created = now, LastModified = now };

        _inception = CreateMovie("Inception", "en");
        _forrestGump = CreateMovie("Forrest Gump", "en");
        _titanic = CreateMovie("Titanic", "en");

        AddActor(_inception, diCaprio, "Cobb");
        AddActor(_titanic, diCaprio, "Jack");
        AddActor(_forrestGump, hanks, "Forrest");

        _inception.Ratings.Add(new UserRating
        {
            Id = Guid.NewGuid(),
            UserId = _userA,
            User = userA,
            MediaId = _inception.Id,
            Media = _inception,
            Value = 9,
            MinimumValue = 0,
            MaximumValue = 10,
            Created = now,
            LastModified = now
        });
        _titanic.Ratings.Add(new UserRating
        {
            Id = Guid.NewGuid(),
            UserId = _userB,
            User = userB,
            MediaId = _titanic.Id,
            Media = _titanic,
            Value = 5,
            MinimumValue = 0,
            MaximumValue = 10,
            Created = now,
            LastModified = now
        });

        _inception.UserMediaStates.Add(new UserMediaState
        {
            Id = Guid.NewGuid(),
            UserId = _userA,
            User = userA,
            MediaId = _inception.Id,
            Media = _inception,
            IsCompleted = true,
            Created = now,
            LastModified = now
        });

        var serie = new Serie
        {
            Id = Guid.NewGuid(),
            Title = "Breaking Bad",
            Created = now,
            LastModified = now
        };
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            Title = "Season 1",
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = 1,
            Created = now,
            LastModified = now
        };
        _breakingBadPilot = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            Title = "Pilot",
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 1,
            Created = now,
            LastModified = now
        };
        season.Episodes.Add(_breakingBadPilot);
        serie.Seasons.Add(season);
        AddActor(serie, cranston, "Walter White");

        _context.Users.AddRange(userA, userB);
        _context.Persons.AddRange(diCaprio, hanks, cranston);
        _context.Medias.AddRange(_inception, _forrestGump, _titanic, serie, season, _breakingBadPilot);
        _context.SaveChanges();
    }

    private static Movie CreateMovie(string title, string language)
    {
        var now = DateTimeOffset.UtcNow;
        return new Movie
        {
            Id = Guid.NewGuid(),
            Title = title,
            OriginalLanguage = language,
            Created = now,
            LastModified = now
        };
    }

    private static void AddActor(BaseMedia media, Person person, string characterName)
    {
        var role = new Actor
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            Person = person,
            MediaId = media.Id,
            Media = media,
            CharacterName = characterName,
            Created = media.Created,
            LastModified = media.LastModified
        };
        media.PersonRoles.Add(role);
        person.Roles.Add(role);
    }
}
