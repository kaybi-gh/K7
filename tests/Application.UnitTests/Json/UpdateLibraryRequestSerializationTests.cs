using System.Text.Json;
using K7.Server.Application.Features.Libraries.Commands.UpdateLibrary;
using K7.Shared.Dtos.Requests;
using K7.Shared.Json;

namespace K7.Server.Application.UnitTests.Json;

public class UpdateLibraryRequestSerializationTests
{
    private static readonly JsonSerializerOptions Options = K7JsonSerializerOptions.CreateDefault();

    [Test]
    public void Serialize_ShouldIncludeDisabledScanSettings()
    {
        var request = new UpdateLibraryRequest
        {
            Title = "Movies",
            RealtimeMonitorEnabled = false,
            AutoScanIntervalHours = 0
        };

        var json = JsonSerializer.Serialize(request, Options);

        json.Should().Contain("false");
        json.Should().Contain("0");
    }

    [Test]
    public void Deserialize_ShouldPreserveDisabledScanSettings()
    {
        const string json = """{"title":"Movies","realtimeMonitorEnabled":false,"autoScanIntervalHours":0}""";

        var command = JsonSerializer.Deserialize<UpdateLibraryCommand>(json, Options);

        command.Should().NotBeNull();
        command!.RealtimeMonitorEnabled.Should().BeFalse();
        command.AutoScanIntervalHours.Should().Be(0);
    }
}
