using System.Text.Json;
using K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

namespace K7.Server.Application.UnitTests.Features.Metadata.Tvdb;

[TestFixture]
public class TvdbApiModelsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Test]
    public void DeserializeSeriesEpisodesPage_ShouldReadEpisodesArray()
    {
        const string json = """
            {
              "status": "success",
              "data": {
                "series": { "id": 102621 },
                "episodes": [
                  {
                    "id": 839151,
                    "seriesId": 102621,
                    "seasonNumber": 1,
                    "number": 1,
                    "name": "Superman (aka The Mad Scientist)",
                    "overview": "Lois Lane heads out.",
                    "aired": "1941-09-26",
                    "runtime": 10,
                    "image": "https://artworks.thetvdb.com/banners/series/102621/episodes/5ef56513ccb17.jpg"
                  }
                ]
              }
            }
            """;

        var payload = JsonSerializer.Deserialize<TvdbApiResponse<TvdbSeriesEpisodesPage>>(json, JsonOptions);

        payload.Should().NotBeNull();
        payload!.Data.Should().NotBeNull();
        payload.Data!.Episodes.Should().HaveCount(1);

        var episode = payload.Data.Episodes![0];
        episode.Id.Should().Be(839151);
        episode.SeasonNumber.Should().Be(1);
        episode.Number.Should().Be(1);
        episode.Name.Should().Be("Superman (aka The Mad Scientist)");
        episode.Overview.Should().Contain("Lois Lane");
        episode.Aired.Should().Be("1941-09-26");
        episode.Runtime.Should().Be(10);
        episode.Image.Should().Contain("artworks.thetvdb.com");
    }
}
