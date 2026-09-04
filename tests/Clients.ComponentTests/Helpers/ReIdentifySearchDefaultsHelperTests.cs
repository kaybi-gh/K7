using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class ReIdentifySearchDefaultsHelperTests
{
    [Test]
    public void FromIndexedFiles_ShouldPreferSeriesTitle_WhenMediaTypeIsSerie()
    {
        var files = new[]
        {
            new IndexedFileDto
            {
                Id = Guid.NewGuid(),
                LibraryId = Guid.NewGuid(),
                Name = "file",
                Extension = ".mkv",
                Path = "/a.mkv",
                Hash = 1,
                Size = 1,
                Identification = new MediaIdentificationDto
                {
                    Title = "Episode Title",
                    SeriesTitle = "Parsed Series",
                    ReleaseYear = new DateOnly(2018, 5, 1)
                }
            }
        };

        var (query, year) = ReIdentifySearchDefaultsHelper.FromIndexedFiles(
            files,
            MediaType.Serie,
            fallbackQuery: "Current Media Title",
            fallbackYear: 2020);

        query.Should().Be("Parsed Series");
        year.Should().Be(2018);
    }

    [Test]
    public void FromIndexedFiles_ShouldPreferPreferredFileIdentification()
    {
        var preferredId = Guid.NewGuid();
        var files = new[]
        {
            new IndexedFileDto
            {
                Id = Guid.NewGuid(),
                LibraryId = Guid.NewGuid(),
                Name = "other",
                Extension = ".mkv",
                Path = "/other.mkv",
                Hash = 1,
                Size = 1,
                Identification = new MediaIdentificationDto { Title = "Other Movie" }
            },
            new IndexedFileDto
            {
                Id = preferredId,
                LibraryId = Guid.NewGuid(),
                Name = "preferred",
                Extension = ".mkv",
                Path = "/preferred.mkv",
                Hash = 2,
                Size = 2,
                Identification = new MediaIdentificationDto
                {
                    Title = "Preferred Movie",
                    ReleaseYear = new DateOnly(1999, 1, 1)
                }
            }
        };

        var (query, year) = ReIdentifySearchDefaultsHelper.FromIndexedFiles(
            files,
            MediaType.Movie,
            preferredIndexedFileId: preferredId,
            fallbackQuery: "Fallback");

        query.Should().Be("Preferred Movie");
        year.Should().Be(1999);
    }

    [Test]
    public void FromIndexedFiles_ShouldUseFallback_WhenNoIdentification()
    {
        var files = new[]
        {
            new IndexedFileDto
            {
                Id = Guid.NewGuid(),
                LibraryId = Guid.NewGuid(),
                Name = "file",
                Extension = ".mkv",
                Path = "/a.mkv",
                Hash = 1,
                Size = 1
            }
        };

        var (query, year) = ReIdentifySearchDefaultsHelper.FromIndexedFiles(
            files,
            MediaType.Movie,
            fallbackQuery: "Fallback Title",
            fallbackYear: 2010);

        query.Should().Be("Fallback Title");
        year.Should().Be(2010);
    }

    [Test]
    public void FromIdentification_ShouldIncludeArtist_WhenMusicAlbum()
    {
        var (query, year) = ReIdentifySearchDefaultsHelper.FromIdentification(
            new MediaIdentificationDto
            {
                Title = "Greatest Hits",
                AlbumName = "Greatest Hits",
                ArtistName = "Jamiroquai",
                ReleaseYear = new DateOnly(2006, 11, 6)
            },
            MediaType.MusicAlbum);

        query.Should().Be("Greatest Hits");
        year.Should().Be(2006);
    }

    [Test]
    public void BuildMusicAlbumLuceneQuery_ShouldMatchAutoIdentifyShape()
    {
        ReIdentifySearchDefaultsHelper.BuildMusicAlbumLuceneQuery("Jamiroquai", "Greatest Hits")
            .Should().Be("release:\"Greatest Hits\" AND artist:\"Jamiroquai\"");
    }

    [Test]
    public void BuildMusicAlbumQuery_ShouldNotDuplicateArtist_WhenAlbumAlreadyPrefixed()
    {
        ReIdentifySearchDefaultsHelper.BuildMusicAlbumQuery("Jamiroquai", "Jamiroquai - Greatest Hits")
            .Should().Be("Jamiroquai - Greatest Hits");
    }

    [Test]
    public void BuildMusicAlbumQuery_ShouldReturnAlbumOnly_WhenArtistMissing()
    {
        ReIdentifySearchDefaultsHelper.BuildMusicAlbumQuery(null, "Greatest Hits")
            .Should().Be("Greatest Hits");
    }

    [Test]
    public void ResolveSourcePath_ShouldReturnMovieFilePath()
    {
        var path = @"D:\Movies\Inception (2010).mkv";
        var files = new[]
        {
            new IndexedFileDto
            {
                Id = Guid.NewGuid(),
                LibraryId = Guid.NewGuid(),
                Name = "Inception (2010)",
                Extension = ".mkv",
                Path = path,
                Hash = 1,
                Size = 1
            }
        };

        ReIdentifySearchDefaultsHelper.ResolveSourcePath(files, MediaType.Movie)
            .Should().Be(path);
    }

    [Test]
    public void ResolveSourcePath_ShouldReturnSerieRoot_WhenSeasonFolderPresent()
    {
        var files = new[]
        {
            new IndexedFileDto
            {
                Id = Guid.NewGuid(),
                LibraryId = Guid.NewGuid(),
                Name = "Show - S01E01",
                Extension = ".mkv",
                Path = Path.Combine("media", "series", "Cool Show", "Season 01", "Show - S01E01.mkv"),
                ParentDirectory = "Season 01",
                Hash = 1,
                Size = 1
            }
        };

        var resolved = ReIdentifySearchDefaultsHelper.ResolveSourcePath(files, MediaType.Serie);

        resolved.Should().Be(Path.Combine("media", "series", "Cool Show"));
    }

    [Test]
    public void ResolveSourcePath_ShouldReturnSerieRoot_WhenReleaseStyleSeasonFolderPresent()
    {
        var files = new[]
        {
            new IndexedFileDto
            {
                Id = Guid.NewGuid(),
                LibraryId = Guid.NewGuid(),
                Name = "01 - Bienvenue",
                Extension = ".mp4",
                Path = Path.Combine(
                    "media",
                    "series",
                    "Warehouse 13",
                    "Warehouse 13 - Saison 01 - DVDRip TrueFrench - Chupacabra",
                    "01 - Bienvenue.mp4"),
                ParentDirectory = "Warehouse 13 - Saison 01 - DVDRip TrueFrench - Chupacabra",
                Hash = 1,
                Size = 1
            }
        };

        var resolved = ReIdentifySearchDefaultsHelper.ResolveSourcePath(files, MediaType.Serie);

        resolved.Should().Be(Path.Combine("media", "series", "Warehouse 13"));
    }

    [Test]
    public void ResolveSourcePath_ShouldReturnPreferredFile_WhenFileScoped()
    {
        var preferredId = Guid.NewGuid();
        var preferredPath = @"/media/series/Cool Show/Season 01/Show - S01E02.mkv";
        var files = new[]
        {
            new IndexedFileDto
            {
                Id = Guid.NewGuid(),
                LibraryId = Guid.NewGuid(),
                Name = "Show - S01E01",
                Extension = ".mkv",
                Path = @"/media/series/Cool Show/Season 01/Show - S01E01.mkv",
                Hash = 1,
                Size = 1
            },
            new IndexedFileDto
            {
                Id = preferredId,
                LibraryId = Guid.NewGuid(),
                Name = "Show - S01E02",
                Extension = ".mkv",
                Path = preferredPath,
                Hash = 2,
                Size = 2
            }
        };

        ReIdentifySearchDefaultsHelper.ResolveSourcePath(files, MediaType.Serie, preferredIndexedFileId: preferredId)
            .Should().Be(preferredPath);
    }
}
