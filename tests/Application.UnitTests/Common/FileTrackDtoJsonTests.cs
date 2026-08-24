using System.Text.Json;
using K7.Shared;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Json;

namespace K7.Server.Application.UnitTests.Common;

public class FileTrackDtoJsonTests
{
    private static readonly JsonSerializerOptions Options = K7JsonSerializerOptions.CreateDefault();

    [Test]
    public void Deserialize_ShouldAcceptSubtitleTrack_WhenCodecPropertyIsMissing()
    {
        const string json = """
            {
              "index": 3,
              "isDefault": false,
              "language": "fr",
              "isTextBased": true,
              "isForced": false,
              "isHearingImpaired": false
            }
            """;

        var track = JsonSerializer.Deserialize<SubtitleFileTrackDto>(json, Options);

        track.Should().NotBeNull();
        track!.Index.Should().Be(3);
        track.Language.Should().Be("fr");
        track.Codec.Should().BeNull();
    }

    [Test]
    public void Deserialize_ShouldAcceptSubtitleTrack_WhenCodecIsNull()
    {
        const string json = """
            {
              "index": 3,
              "isDefault": false,
              "codec": null,
              "language": "fr",
              "isTextBased": true
            }
            """;

        var track = JsonSerializer.Deserialize<SubtitleFileTrackDto>(json, Options);

        track.Should().NotBeNull();
        track!.Codec.Should().BeNull();
    }

    [Test]
    public void FormatSubtitleLabel_ShouldOmitCodec_WhenMissing()
    {
        var track = new SubtitleFileTrackDto
        {
            Index = 1,
            Language = "fr",
            Codec = null,
            IsTextBased = true
        };

        var label = AudioTrackDisplayHelper.FormatSubtitleLabel(track, "Full");
        label.Should().EndWith(" - Full");
        label.Should().NotContain("(");
    }
}
