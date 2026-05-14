using K7.Server.Application.Extensions;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.ValueObjects;
using K7.Tests.Helpers.Fixtures;
using K7.Tests.Helpers.Helpers;

namespace K7.Server.Application.UnitTests.Extensions;

public class IndexedFileExtensionsTests : FileFixture
{
    [TestCase("", "1884 (1995).mp4", "1884", 1995)]
    [TestCase("", "1884 - 1995.mp4", "1884", 1995)]
    [TestCase("", "Movie (1990).mp4", "Movie", 1990)]
    [TestCase("", "Movie - 1990.mp4", "Movie", 1990)]
    [TestCase("", "Movie 1884 (1995).mp4", "Movie 1884", 1995)]
    [TestCase("", "Movie 1990.mp4", "Movie", 1990)]
    [TestCase("", "Movie(1990).mp4", "Movie", 1990)]
    [TestCase("", "Movie.1990.noise.mp4", "Movie", 1990)]
    [TestCase("", "Movie.noise.1990.mp4", "Movie.noise", 1990)]
    [TestCase("", "Movie.noise.1990.noise.mp4", "Movie.noise", 1990)]
    [TestCase("", "The incredible movie (1995).mp4", "The incredible movie", 1995)]
    [TestCase("AnotherMovie", "Movie - 1990.mp4", "Movie", 1990)]
    [TestCase("AnotherMovie", "Movie - 1990.mp4", "Movie", 1990)]
    [TestCase("Movie (2000)", "Movie (2001).mkv", "Movie", 2001)]
    [TestCase("Movie (2000)", "Movie.mkv", "Movie", 2000)]
    [TestCase("Movie (2000)", "RandomFileTitle.mkv", "Movie", 2000)]
    [TestCase("Movie (2005)", "Movie cd1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie part1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie-cd1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie-part1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie.cd1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie.part1.avi", "Movie", 2005)]
    [TestCase(null, "Movie(1990).mp4", "Movie", 1990)]
    public void ShouldParseMovieFileCorrectly(string? parentDirectory, string fileName, string expectedMovieTitle, int expectedReleaseYear)
    {
        // Arrange
        var path = string.IsNullOrEmpty(parentDirectory) ? fileName : Path.Combine(parentDirectory, fileName);
        var fileInfo = FileHelper.CreateTestFile(path, "content");
        var library = new Library()
        {
            Id = Guid.NewGuid(),
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RootPath = FileHelper.TestDirectoryPath,
            Title = "Movies"
        };
        var indexedFile = fileInfo.ToIndexedFile(library.Id);

        // Act
        indexedFile!.TryIdentifyMovie(out MediaIdentification? identification);

        // Assert
        //indexedFile!.IsIdentified.Should().BeTrue();
        identification!.Title.Should().Be(expectedMovieTitle);
        identification!.ReleaseYear!.Value.Year.Should().Be(expectedReleaseYear);
    }

    [TestCase("Artist", "Album (2020)", "01 - Song Title.flac", "Song Title", 1, "Album", "Artist", 2020)]
    [TestCase("Artist", "Album (2020)", "02. Another Song.mp3", "Another Song", 2, "Album", "Artist", 2020)]
    [TestCase("Artist", "Album", "03 Song Title.ogg", "Song Title", 3, "Album", "Artist", null)]
    [TestCase("Artist", "Album - 2019", "1-Track.m4a", "Track", 1, "Album", "Artist", 2019)]
    [TestCase(null, "Album (2020)", "01 - Song Title.flac", "Song Title", 1, "Album", null, 2020)]
    [TestCase(null, "Album", "Song Title.flac", "Song Title", null, "Album", null, null)]
    [TestCase(null, null, "01 - Song Title.opus", "Song Title", 1, null, null, null)]
    [TestCase(null, null, "Song Title.mp3", "Song Title", null, null, null, null)]
    [TestCase("Artist", "Album (2020)", "12 - Song (feat. Other).flac", "Song (feat. Other)", 12, "Album", "Artist", 2020)]
    public void ShouldParseMusicTrackFileCorrectly(string? grandparentDirectory, string? parentDirectory, string fileName,
        string expectedTitle, int? expectedTrackNumber, string? expectedAlbumName, string? expectedArtistName, int? expectedReleaseYear)
    {
        // Arrange
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(grandparentDirectory)) parts.Add(grandparentDirectory);
        if (!string.IsNullOrEmpty(parentDirectory)) parts.Add(parentDirectory);
        parts.Add(fileName);

        var path = Path.Combine(parts.ToArray());
        var fileInfo = FileHelper.CreateTestFile(path, "content");
        var library = new Library()
        {
            Id = Guid.NewGuid(),
            MediaType = LibraryMediaType.Music,
            MetadataProviderName = "musicbrainz",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RootPath = FileHelper.TestDirectoryPath,
            Title = "Music"
        };
        var indexedFile = fileInfo.ToIndexedFile(library.Id)!;

        // Act
        var result = indexedFile.TryIdentifyMusicTrack(library, [indexedFile]);

        // Assert
        result.Should().BeTrue();
        indexedFile.Identification.Should().NotBeNull();
        indexedFile.Identification!.Title.Should().Be(expectedTitle);
        indexedFile.Identification!.TrackNumber.Should().Be(expectedTrackNumber);
        indexedFile.Identification!.AlbumName.Should().Be(expectedAlbumName);
        indexedFile.Identification!.ArtistName.Should().Be(expectedArtistName);
        if (expectedReleaseYear.HasValue)
        {
            indexedFile.Identification!.ReleaseYear!.Value.Year.Should().Be(expectedReleaseYear.Value);
        }
        else
        {
            indexedFile.Identification!.ReleaseYear.Should().BeNull();
        }
    }

    #region Serie Episode Parsing - SxxExx patterns

    [TestCase("Breaking Bad", "Season 1", "Breaking.Bad.S01E01.720p.mkv", "Breaking Bad", 1, 1, null, null)]
    [TestCase("Breaking Bad", "Season 1", "Breaking Bad S01E02 - Cat's in the Bag.mkv", "Breaking Bad", 1, 2, null, null)]
    [TestCase("Game of Thrones (2011)", "Season 3", "S03E09.mkv", "Game of Thrones", 3, 9, null, 2011)]
    [TestCase("The Office", null, "The.Office.S02E05.720p.mkv", "The Office", 2, 5, null, null)]
    [TestCase(null, null, "Show.Name.S01E01.Episode.Title.720p.mkv", "Show Name", 1, 1, null, null)]
    [TestCase("Show", null, "S1E1.mkv", "Show", 1, 1, null, null)]
    [TestCase("Show", "Season 01", "S01E100.mkv", "Show", 1, 100, null, null)]
    public void ShouldParseEpisodeSxxExx(string? grandparent, string? parent, string fileName,
        string expectedTitle, int expectedSeason, int expectedEpisode, int? expectedAbsolute, int? expectedYear)
    {
        AssertEpisodeParsing(grandparent, parent, fileName, expectedTitle, expectedSeason, expectedEpisode, expectedAbsolute, expectedYear);
    }

    #endregion

    #region Serie Episode Parsing - NxNN patterns

    [TestCase("Show", "Season 1", "1x01.mkv", "Show", 1, 1, null, null)]
    [TestCase("Show", null, "Show.Name.2x05.mkv", "Show Name", 2, 5, null, null)]
    public void ShouldParseEpisodeNxNN(string? grandparent, string? parent, string fileName,
        string expectedTitle, int expectedSeason, int expectedEpisode, int? expectedAbsolute, int? expectedYear)
    {
        AssertEpisodeParsing(grandparent, parent, fileName, expectedTitle, expectedSeason, expectedEpisode, expectedAbsolute, expectedYear);
    }

    #endregion

    #region Serie Episode Parsing - Multi-episode (takes first only)

    [TestCase("Show", "Season 1", "Show.S01E01-E03.mkv", "Show", 1, 1, null, null)]
    [TestCase("Show", "Season 1", "Show.S01E01E02.mkv", "Show", 1, 1, null, null)]
    public void ShouldParseMultiEpisodeTakingFirst(string? grandparent, string? parent, string fileName,
        string expectedTitle, int expectedSeason, int expectedEpisode, int? expectedAbsolute, int? expectedYear)
    {
        AssertEpisodeParsing(grandparent, parent, fileName, expectedTitle, expectedSeason, expectedEpisode, expectedAbsolute, expectedYear);
    }

    #endregion

    #region Serie Episode Parsing - Absolute numbering (anime)

    [TestCase("One Piece", null, "One Piece - 1001.mkv", "One Piece", null, null, 1001, null)]
    [TestCase("Naruto", null, "Naruto - 220.mkv", "Naruto", null, null, 220, null)]
    [TestCase("Bleach", "Season 1", "Bleach - 50.mkv", "Bleach", 1, null, 50, null)]
    public void ShouldParseEpisodeAbsolute(string? grandparent, string? parent, string fileName,
        string expectedTitle, int? expectedSeason, int? expectedEpisode, int? expectedAbsolute, int? expectedYear)
    {
        AssertEpisodeParsing(grandparent, parent, fileName, expectedTitle, expectedSeason, expectedEpisode, expectedAbsolute, expectedYear);
    }

    #endregion

    #region Serie Episode Parsing - Anime with fansub tags

    [TestCase("Frieren", null, "[SubGroup] Frieren - Beyond Journey's End - 15 [1080p][AABBCCDD].mkv", "Frieren - Beyond Journey's End", null, null, 15, null)]
    [TestCase("Show", null, "[Group] Show Name - 01 [720p].mkv", "Show Name", null, null, 1, null)]
    public void ShouldParseAnimeWithFansubTags(string? grandparent, string? parent, string fileName,
        string expectedTitle, int? expectedSeason, int? expectedEpisode, int? expectedAbsolute, int? expectedYear)
    {
        AssertEpisodeParsing(grandparent, parent, fileName, expectedTitle, expectedSeason, expectedEpisode, expectedAbsolute, expectedYear);
    }

    #endregion

    #region Serie Episode Parsing - Season from folder

    [TestCase("Show", "Season 0", "Show.S00E01.mkv", "Show", 0, 1, null, null)]
    [TestCase("Show", "Specials", "Show.S00E01.mkv", "Show", 0, 1, null, null)]
    [TestCase("Show (2020)", "Season 2", "S02E01.mkv", "Show", 2, 1, null, 2020)]
    public void ShouldParseEpisodeWithSeasonFolder(string? grandparent, string? parent, string fileName,
        string expectedTitle, int expectedSeason, int expectedEpisode, int? expectedAbsolute, int? expectedYear)
    {
        AssertEpisodeParsing(grandparent, parent, fileName, expectedTitle, expectedSeason, expectedEpisode, expectedAbsolute, expectedYear);
    }

    #endregion

    #region Serie Episode Parsing - False positives (should NOT parse)

    [TestCase("Movies", null, "Movie.1080p.BluRay.mkv")]
    [TestCase("Movies", null, "Movie.1920x1080.mkv")]
    [TestCase("Movies", null, "Movie.2160p.mkv")]
    [TestCase("Movies", null, "Movie.4K.mkv")]
    public void ShouldNotParseResolutionAsEpisode(string? grandparent, string? parent, string fileName)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(grandparent)) parts.Add(grandparent);
        if (!string.IsNullOrEmpty(parent)) parts.Add(parent);
        parts.Add(fileName);

        var path = Path.Combine(parts.ToArray());
        var fileInfo = FileHelper.CreateTestFile(path, "content");
        var library = new Library
        {
            Id = Guid.NewGuid(),
            MediaType = LibraryMediaType.Serie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RootPath = FileHelper.TestDirectoryPath,
            Title = "TV Shows"
        };
        var indexedFile = fileInfo.ToIndexedFile(library.Id)!;

        var result = indexedFile.TryIdentifySerieEpisode(library, [indexedFile]);

        result.Should().BeFalse();
    }

    #endregion

    private void AssertEpisodeParsing(string? grandparent, string? parent, string fileName,
        string expectedTitle, int? expectedSeason, int? expectedEpisode, int? expectedAbsolute, int? expectedYear)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(grandparent)) parts.Add(grandparent);
        if (!string.IsNullOrEmpty(parent)) parts.Add(parent);
        parts.Add(fileName);

        var path = Path.Combine(parts.ToArray());
        var fileInfo = FileHelper.CreateTestFile(path, "content");
        var library = new Library
        {
            Id = Guid.NewGuid(),
            MediaType = LibraryMediaType.Serie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RootPath = FileHelper.TestDirectoryPath,
            Title = "TV Shows"
        };
        var indexedFile = fileInfo.ToIndexedFile(library.Id)!;

        var result = indexedFile.TryIdentifySerieEpisode(library, [indexedFile]);

        result.Should().BeTrue();
        indexedFile.Identification.Should().NotBeNull();
        indexedFile.Identification!.SeriesTitle.Should().Be(expectedTitle);

        if (expectedSeason.HasValue)
            indexedFile.Identification.SeasonNumber.Should().Be(expectedSeason.Value);
        else
            indexedFile.Identification.SeasonNumber.Should().BeNull();

        if (expectedEpisode.HasValue)
            indexedFile.Identification.EpisodeNumber.Should().Be(expectedEpisode.Value);
        else
            indexedFile.Identification.EpisodeNumber.Should().BeNull();

        if (expectedAbsolute.HasValue)
            indexedFile.Identification.AbsoluteNumber.Should().Be(expectedAbsolute.Value);
        else
            indexedFile.Identification.AbsoluteNumber.Should().BeNull();

        if (expectedYear.HasValue)
            indexedFile.Identification.ReleaseYear!.Value.Year.Should().Be(expectedYear.Value);
        else
            indexedFile.Identification.ReleaseYear.Should().BeNull();
    }
}
