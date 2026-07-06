using System.Text.Json;
using K7.Server.Application.Features.Libraries.Commands.CreateLibrary;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using K7.Shared.Json;

namespace K7.Server.Application.UnitTests.Json;

public class CreateLibraryRequestSerializationTests
{
    private static readonly JsonSerializerOptions Options = K7JsonSerializerOptions.CreateDefault();

    [Test]
    public void Serialize_ShouldIncludeDisabledScanSettings()
    {
        var request = CreateRequest(realtimeMonitorEnabled: false, autoScanIntervalHours: 0);

        var json = JsonSerializer.Serialize(request, Options);

        json.Should().Contain("false");
        json.Should().Contain("0");
    }

    [Test]
    public void Deserialize_ShouldPreserveDisabledScanSettings()
    {
        var request = CreateRequest(realtimeMonitorEnabled: false, autoScanIntervalHours: 0);
        var json = JsonSerializer.Serialize(request, Options);

        var command = JsonSerializer.Deserialize<CreateLibraryCommand>(json, Options);

        command.Should().NotBeNull();
        command!.RealtimeMonitorEnabled.Should().BeFalse();
        command.AutoScanIntervalHours.Should().Be(0);
    }

    [Test]
    public void Deserialize_ShouldUseDefaultsWhenScanSettingsAreOmitted()
    {
        const string json = """
            {
              "title": "Movies",
              "mediaType": "Movie",
              "rootPath": "/movies",
              "metadataProviderName": "tmdb",
              "metadataLanguage": "fr",
              "metadataFallbackLanguage": "en"
            }
            """;

        var command = JsonSerializer.Deserialize<CreateLibraryCommand>(json, Options);

        command.Should().NotBeNull();
        command!.RealtimeMonitorEnabled.Should().BeTrue();
        command.AutoScanIntervalHours.Should().Be(6);
    }

    private static CreateLibraryRequest CreateRequest(bool realtimeMonitorEnabled, int autoScanIntervalHours) => new()
    {
        Title = "Movies",
        MediaType = LibraryMediaType.Movie,
        RootPath = "/movies",
        MetadataProviderName = "tmdb",
        MetadataLanguage = "fr",
        MetadataFallbackLanguage = "en",
        RealtimeMonitorEnabled = realtimeMonitorEnabled,
        AutoScanIntervalHours = autoScanIntervalHours
    };
}
