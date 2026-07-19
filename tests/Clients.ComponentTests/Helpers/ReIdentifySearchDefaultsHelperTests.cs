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
}
