using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.LastVideoTrackSelection.Commands.UpsertLastVideoTrackSelection;
using K7.Server.Application.Features.VideoPlayerSettings.Queries.GetEffectiveVideoPlayerSettings;
using K7.Server.Application.Services;
using MediatR;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Constants;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.LastVideoTrackSelection;

[TestFixture]
public class UpsertLastVideoTrackSelectionCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUserSettingsService _userSettings = null!;
    private ISharedProfileSettingsService _sharedProfileSettings = null!;
    private IMediaAccessGuard _accessGuard = null!;
    private IUser _currentUser = null!;
    private ISender _sender = null!;
    private UpsertLastVideoTrackSelectionCommandHandler _handler = null!;
    private Guid _userId;
    private Guid _serieId;
    private Guid _episodeId;
    private Guid _movieId;
    private Guid _sharedProfileId;

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
        _serieId = Guid.NewGuid();
        _episodeId = Guid.NewGuid();
        _movieId = Guid.NewGuid();
        _sharedProfileId = Guid.NewGuid();

        var serie = new Serie { Id = _serieId, Title = "Show", SortTitle = "Show" };
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1"
        };
        var episode = new SerieEpisode
        {
            Id = _episodeId,
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 1,
            Title = "Ep 1",
            SortTitle = "Ep 1"
        };
        var movie = new Movie { Id = _movieId, Title = "Film", SortTitle = "Film" };

        _context.Medias.AddRange(serie, season, episode, movie);
        var coViewerId = Guid.NewGuid();
        _context.Users.AddRange(
            new User { Id = _userId, DisplayName = "viewer" },
            new User { Id = coViewerId, DisplayName = "partner" });
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = _sharedProfileId,
            Name = "Couple",
            HostUserId = _userId,
            CreatedByUserId = _userId,
            Members =
            [
                new SharedProfileMember { Id = Guid.NewGuid(), SharedProfileId = _sharedProfileId, UserId = _userId },
                new SharedProfileMember { Id = Guid.NewGuid(), SharedProfileId = _sharedProfileId, UserId = coViewerId }
            ]
        });
        _context.SaveChanges();

        _userSettings = Substitute.For<IUserSettingsService>();
        _sharedProfileSettings = Substitute.For<ISharedProfileSettingsService>();
        _accessGuard = Substitute.For<IMediaAccessGuard>();
        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);
        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<GetEffectiveVideoPlayerSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new VideoPlayerSettingsDto { RememberTrackSelection = true });

        _handler = new UpsertLastVideoTrackSelectionCommandHandler(
            _context,
            _userSettings,
            _sharedProfileSettings,
            _accessGuard,
            _currentUser,
            _sender);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldStoreUnderSerieKey_WhenMediaIsEpisode()
    {
        var selection = new LastVideoTrackSelectionDto
        {
            AudioLanguage = "ja",
            SubtitleLanguage = "fr",
            IsForced = true
        };

        await _handler.Handle(
            new UpsertLastVideoTrackSelectionCommand { MediaId = _episodeId, Selection = selection },
            CancellationToken.None);

        var expectedKey = UserPreferenceKeys.LastVideoTrackSelectionForSerie(_serieId);
        await _userSettings.Received(1).SetAsync(
            _userId,
            Arg.Is<SettingKey<string>>(k => k.Name == expectedKey),
            Arg.Is<string>(json => JsonSerializer.Deserialize<LastVideoTrackSelectionDto>(json)!.AudioLanguage == "ja"),
            Arg.Any<CancellationToken>());
        await _sharedProfileSettings.DidNotReceive()
            .SetAsync(Arg.Any<Guid>(), Arg.Any<SettingKey<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _accessGuard.Received(1).EnsureAccessAsync(_episodeId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldStoreUnderMovieKey_WhenMediaIsMovie()
    {
        var selection = new LastVideoTrackSelectionDto { AudioLanguage = "en", SubtitlesOff = true };

        await _handler.Handle(
            new UpsertLastVideoTrackSelectionCommand { MediaId = _movieId, Selection = selection },
            CancellationToken.None);

        var expectedKey = UserPreferenceKeys.LastVideoTrackSelectionForMovie(_movieId);
        await _userSettings.Received(1).SetAsync(
            _userId,
            Arg.Is<SettingKey<string>>(k => k.Name == expectedKey),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldStoreOnSharedProfile_WhenSharedProfileActive()
    {
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns(_sharedProfileId);

        var selection = new LastVideoTrackSelectionDto { AudioLanguage = "fr" };

        await _handler.Handle(
            new UpsertLastVideoTrackSelectionCommand { MediaId = _movieId, Selection = selection },
            CancellationToken.None);

        var expectedKey = UserPreferenceKeys.LastVideoTrackSelectionForMovie(_movieId);
        await _sharedProfileSettings.Received(1).SetAsync(
            _sharedProfileId,
            Arg.Is<SettingKey<string>>(k => k.Name == expectedKey),
            Arg.Is<string>(json => JsonSerializer.Deserialize<LastVideoTrackSelectionDto>(json)!.AudioLanguage == "fr"),
            Arg.Any<CancellationToken>());
        await _userSettings.DidNotReceive()
            .SetAsync(Arg.Any<Guid>(), Arg.Any<SettingKey<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldSkipPersist_WhenRememberTrackSelectionDisabled()
    {
        _sender.Send(Arg.Any<GetEffectiveVideoPlayerSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new VideoPlayerSettingsDto { RememberTrackSelection = false });

        await _handler.Handle(
            new UpsertLastVideoTrackSelectionCommand
            {
                MediaId = _movieId,
                Selection = new LastVideoTrackSelectionDto { AudioLanguage = "en" }
            },
            CancellationToken.None);

        await _userSettings.DidNotReceive()
            .SetAsync(Arg.Any<Guid>(), Arg.Any<SettingKey<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sharedProfileSettings.DidNotReceive()
            .SetAsync(Arg.Any<Guid>(), Arg.Any<SettingKey<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
