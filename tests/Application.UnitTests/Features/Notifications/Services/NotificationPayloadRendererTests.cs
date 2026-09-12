using System.Text.Json;
using K7.Server.Application.Features.Notifications.Services;
using AwesomeAssertions;

namespace K7.Server.Application.UnitTests.Features.Notifications.Services;

[TestFixture]
public class NotificationPayloadRendererTests
{
    private readonly NotificationPayloadRenderer _renderer = new();

    [Test]
    public void RenderPlain_ShouldReplaceSimplePlaceholder()
    {
        var data = new Dictionary<string, object?> { ["Peer.Name"] = "Home" };
        _renderer.RenderPlain("Peer {{Peer.Name}} changed", data)
            .Should().Be("Peer Home changed");
    }

    [Test]
    public void RenderPlain_ShouldMapBooleanValues()
    {
        var data = new Dictionary<string, object?>
        {
            ["Peer.Name"] = "Home",
            ["Succeeded"] = true
        };

        _renderer.RenderPlain(
                "Peer {{Peer.Name}} is now {{Succeeded|true=online|false=offline}}",
                data)
            .Should().Be("Peer Home is now online");
    }

    [Test]
    public void RenderPlain_ShouldUseFallbackMapping()
    {
        var data = new Dictionary<string, object?> { ["State"] = "Buffering" };
        _renderer.RenderPlain("{{State|Playing=started|Paused=paused|*=updated}}", data)
            .Should().Be("updated");
    }

    [Test]
    public void Render_ShouldJsonEscapeMappedValue()
    {
        var data = new Dictionary<string, object?> { ["Title"] = "A \"quote\"" };
        var rendered = _renderer.Render("{\"t\":\"{{Title}}\"}", data);
        rendered.Should().StartWith("{\"t\":\"");
        rendered.Should().EndWith("\"}");
        rendered.Should().NotContain("{{Title}}");
        JsonDocument.Parse(rendered).RootElement.GetProperty("t").GetString()
            .Should().Be("A \"quote\"");
    }

    [Test]
    public void Render_ShouldInsertRawJsonNumbers_WhenTripleBraces()
    {
        var data = new Dictionary<string, object?>
        {
            ["PositionTicks"] = 1_200_000_000L,
            ["Year"] = (int?)null
        };

        var rendered = _renderer.Render(
            "{\"pos\":{{{PositionTicks}}},\"year\":{{{Year|empty=null}}}}",
            data);

        rendered.Should().Be("{\"pos\":1200000000,\"year\":null}");
        JsonDocument.Parse(rendered);
    }

    [Test]
    public void Render_ShouldKeepNumber_WhenEmptyFallbackAndValuePresent()
    {
        var data = new Dictionary<string, object?>
        {
            ["SeasonNumber"] = 7,
            ["EpisodeNumber"] = 2
        };

        var rendered = _renderer.Render(
            "{\"s\":{{{SeasonNumber|empty=null}}},\"e\":{{{EpisodeNumber|empty=null}}}}",
            data);

        rendered.Should().Be("{\"s\":7,\"e\":2}");
        JsonDocument.Parse(rendered);
    }

    [Test]
    public void TryResolve_ShouldReturnFalse_WhenKeyMissing()
    {
        NotificationPayloadRenderer.TryResolve("Missing", new Dictionary<string, object?>(), out _)
            .Should().BeFalse();
    }
}
