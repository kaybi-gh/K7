using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.UnitTests.Common.Mappings;

public class MediaMappingsTests
{
    [Test]
    public void ToMediaDto_ShouldMapMovieFields()
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Title",
            SortTitle = "Sort",
            OriginalTitle = "Original",
            ReleaseDate = new DateOnly(2021, 5, 1),
            Overview = "Overview",
            Tagline = "Tag",
            OriginalLanguage = "en",
            Budget = 10,
            Revenue = 20
        };
        movie.LockField(nameof(Movie.Title));

        var dto = (MovieDto)movie.ToMediaDto();

        dto.Id.Should().Be(movie.Id);
        dto.Title.Should().Be("Title");
        dto.SortTitle.Should().Be("Sort");
        dto.OriginalTitle.Should().Be("Original");
        dto.ReleaseDate.Should().Be(movie.ReleaseDate);
        dto.Overview.Should().Be("Overview");
        dto.TagLine.Should().Be("Tag");
        dto.OriginalLanguage.Should().Be("en");
        dto.Budget.Should().Be(10);
        dto.Revenue.Should().Be(20);
        dto.LockedFields.Should().Contain(nameof(Movie.Title));
        dto.Genres.Should().BeEmpty();
        dto.IndexedFiles.Should().BeEmpty();
    }

    [Test]
    public void ToLiteMediaDto_ShouldMapMinimalMovieFields()
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Lite",
            ReleaseDate = new DateOnly(2019, 1, 1)
        };

        var dto = movie.ToLiteMediaDto();

        dto.Id.Should().Be(movie.Id);
        dto.Title.Should().Be("Lite");
        dto.Should().BeOfType<LiteMovieDto>();
    }

    [Test]
    public void ToMediaDto_ShouldCountOnlyPlayableEpisodes_OnSerieSeasons()
    {
        var serie = new Serie { Id = Guid.NewGuid(), Title = "Show", SortTitle = "Show" };
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1"
        };
        serie.Seasons.Add(season);

        var playable = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 1,
            Title = "E1",
            SortTitle = "E1",
            IndexedFiles =
            [
                new IndexedFile
                {
                    Id = Guid.NewGuid(),
                    LibraryId = Guid.NewGuid(),
                    Name = "e1.mkv",
                    Extension = ".mkv",
                    Path = "/e1.mkv",
                    Hash = 1,
                    Size = 1
                }
            ]
        };
        var orphan = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 2,
            Title = "E2",
            SortTitle = "E2",
            IndexedFiles = [],
            RemoteIndexedFiles = []
        };
        season.Episodes.Add(playable);
        season.Episodes.Add(orphan);

        var dto = (SerieDto)serie.ToMediaDto();

        dto.Seasons.Should().ContainSingle()
            .Which.EpisodeCount.Should().Be(1);
    }

    [Test]
    public void ToLiteMediaDto_ShouldCountOnlyPlayableEpisodes_OnSerieSeason()
    {
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = Guid.NewGuid(),
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1",
            Episodes =
            [
                new SerieEpisode
                {
                    Id = Guid.NewGuid(),
                    EpisodeNumber = 1,
                    Title = "E1",
                    IndexedFiles = [],
                    RemoteIndexedFiles = []
                }
            ]
        };

        var dto = (LiteSerieSeasonDto)season.ToLiteMediaDto();

        dto.EpisodeCount.Should().Be(0);
    }

    [Test]
    public void ToMediaDto_ShouldOverlayItemBookmarkResumePosition()
    {
        var episodeId = Guid.NewGuid();
        var episode = new SerieEpisode
        {
            Id = episodeId,
            Title = "E1",
            SortTitle = "E1",
            EpisodeNumber = 1
        };
        episode.UserMediaStates.Add(new UserMediaState
        {
            UserId = Guid.NewGuid(),
            MediaId = episodeId,
            IsCompleted = false,
            LastInteractedAt = DateTime.UtcNow
        });

        var bookmarks = new Dictionary<Guid, ItemPlaybackBookmark>
        {
            [episodeId] = new ItemPlaybackBookmark
            {
                MediaId = episodeId,
                PositionSeconds = 420,
                DurationSeconds = 2100,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var dto = (SerieEpisodeDto)episode.ToMediaDto(bookmarks);

        dto.UserState.Should().NotBeNull();
        dto.UserState!.LastPlaybackPosition.Should().Be(420);
        dto.UserState.ProgressPercentage.Should().BeApproximately(20, 0.01);
        dto.UserState.IsCompleted.Should().BeFalse();
    }

    [Test]
    public void ToMediaDto_ShouldOverlayNestedEpisodeBookmark_OnSeason()
    {
        var episodeId = Guid.NewGuid();
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = Guid.NewGuid(),
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1"
        };
        var episode = new SerieEpisode
        {
            Id = episodeId,
            SerieId = season.SerieId,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 2,
            Title = "E2",
            SortTitle = "E2"
        };
        episode.UserMediaStates.Add(new UserMediaState
        {
            UserId = Guid.NewGuid(),
            MediaId = episodeId,
            IsCompleted = false
        });
        season.Episodes.Add(episode);

        var bookmarks = new Dictionary<Guid, ItemPlaybackBookmark>
        {
            [episodeId] = new ItemPlaybackBookmark
            {
                MediaId = episodeId,
                PositionSeconds = 90,
                DurationSeconds = 1800,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var dto = (SerieSeasonDto)season.ToMediaDto(bookmarks);

        dto.Episodes.Should().ContainSingle();
        dto.Episodes![0].UserState!.LastPlaybackPosition.Should().Be(90);
    }

    [Test]
    public void ToMediaDto_ShouldReturnZeroResumePosition_WhenBookmarkMissing()
    {
        var episode = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            Title = "E1",
            SortTitle = "E1",
            EpisodeNumber = 1
        };
        episode.UserMediaStates.Add(new UserMediaState
        {
            UserId = Guid.NewGuid(),
            MediaId = episode.Id,
            IsCompleted = false
        });

        var dto = (SerieEpisodeDto)episode.ToMediaDto();

        dto.UserState.Should().NotBeNull();
        dto.UserState!.LastPlaybackPosition.Should().Be(0);
        dto.UserState.ProgressPercentage.Should().Be(0);
    }
}
