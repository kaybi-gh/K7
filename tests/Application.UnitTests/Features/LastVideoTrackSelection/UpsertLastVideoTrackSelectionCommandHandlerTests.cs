using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.LastVideoTrackSelection.Commands.UpsertLastVideoTrackSelection;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
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
    private IMediaAccessGuard _accessGuard = null!;
    private IUser _currentUser = null!;
    private UpsertLastVideoTrackSelectionCommandHandler _handler = null!;
    private Guid _userId;
    private Guid _serieId;
    private Guid _episodeId;
    private Guid _movieId;

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
        _context.SaveChanges();

        _userSettings = Substitute.For<IUserSettingsService>();
        _accessGuard = Substitute.For<IMediaAccessGuard>();
        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);

        _handler = new UpsertLastVideoTrackSelectionCommandHandler(
            _context,
            _userSettings,
            _accessGuard,
            _currentUser);
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
}
