using System.Text.Json;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;
using K7.Shared.Json;

namespace K7.Server.Application.UnitTests.Json;

public class HomeLayoutDtoSerializationTests
{
    private static readonly JsonSerializerOptions Options = K7JsonSerializerOptions.CreateDefault();

    [Test]
    public void RoundTrip_ShouldPreserveRowsAndEnumCollections()
    {
        var layout = new HomeLayoutDto
        {
            Rows =
            [
                new HomeRowConfigDto
                {
                    Id = Guid.NewGuid(),
                    Title = "Continue Watching",
                    DisplayType = HomeRowDisplayType.Carousel,
                    LibraryIds = [Guid.NewGuid(), Guid.NewGuid()],
                    MediaTypes = [MediaType.Movie, MediaType.Serie],
                    OrderBy = [MediaOrderingOption.CreatedDesc, MediaOrderingOption.LocalRatingDesc],
                    PageSize = 15,
                    ContinueWatching = true,
                    IsVisible = true,
                    Order = 0
                },
                new HomeRowConfigDto
                {
                    Id = Guid.NewGuid(),
                    Title = "Recently Added",
                    DisplayType = HomeRowDisplayType.Carousel,
                    LibraryIds = null,
                    MediaTypes = null,
                    OrderBy = null,
                    PageSize = 20,
                    ContinueWatching = false,
                    IsVisible = false,
                    Order = 1
                }
            ]
        };

        var json = JsonSerializer.Serialize(layout, Options);
        var deserialized = JsonSerializer.Deserialize<HomeLayoutDto>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.Rows.Should().HaveCount(2);

        var firstRow = deserialized.Rows[0];
        firstRow.Id.Should().Be(layout.Rows[0].Id);
        firstRow.Title.Should().Be("Continue Watching");
        firstRow.DisplayType.Should().Be(HomeRowDisplayType.Carousel);
        firstRow.LibraryIds.Should().BeEquivalentTo(layout.Rows[0].LibraryIds);
        firstRow.MediaTypes.Should().BeEquivalentTo([MediaType.Movie, MediaType.Serie]);
        firstRow.OrderBy.Should().BeEquivalentTo([MediaOrderingOption.CreatedDesc, MediaOrderingOption.LocalRatingDesc]);
        firstRow.PageSize.Should().Be(15);
        firstRow.ContinueWatching.Should().BeTrue();
        firstRow.IsVisible.Should().BeTrue();

        var secondRow = deserialized.Rows[1];
        secondRow.LibraryIds.Should().BeNull();
        secondRow.MediaTypes.Should().BeNull();
        secondRow.OrderBy.Should().BeNull();
        secondRow.ContinueWatching.Should().BeFalse();
    }

    [Test]
    public void Serialize_ShouldUseCamelCasePropertyNames_AndStringEnumValues()
    {
        var layout = new HomeLayoutDto
        {
            Rows =
            [
                new HomeRowConfigDto
                {
                    Id = Guid.NewGuid(),
                    Title = "Movies",
                    DisplayType = HomeRowDisplayType.Carousel,
                    MediaTypes = [MediaType.Movie],
                    PageSize = 20,
                    ContinueWatching = false,
                    IsVisible = true,
                    Order = 0
                }
            ]
        };

        var json = JsonSerializer.Serialize(layout, Options);

        json.Should().Contain("\"mediaTypes\":[\"Movie\"]");
        json.Should().Contain("\"displayType\":\"Carousel\"");
    }
}
