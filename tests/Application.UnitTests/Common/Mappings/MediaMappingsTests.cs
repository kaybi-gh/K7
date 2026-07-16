using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Medias;
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
}
