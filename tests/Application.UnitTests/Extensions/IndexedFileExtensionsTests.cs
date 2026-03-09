using System.Drawing;
using FFMpegCore;
using MediaServer.Application.Extensions;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Domain.ValueObjects;
using MediaServer.Tests.Helpers.Fixtures;
using MediaServer.Tests.Helpers.Helpers;

namespace MediaServer.Application.UnitTests.Extensions;

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
}
