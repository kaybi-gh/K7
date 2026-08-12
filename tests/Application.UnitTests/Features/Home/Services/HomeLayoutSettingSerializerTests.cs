using System.Text.Json;
using K7.Server.Application.Features.Home.Services;
using K7.Shared.Dtos.Home;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Features.Home.Services;

public class HomeLayoutSettingSerializerTests
{
    private static readonly Guid LibraryA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LibraryB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Test]
    public void TryDeserialize_ShouldReadDoubleEncodedSettingValue()
    {
        var layout = new HomeLayoutDto
        {
            Rows = [CreateRow("Movies", [LibraryA, LibraryB])]
        };

        // Same double-encoding used by Update*HomeLayout + SettingKey<string>.SetAsync
        var stored = JsonSerializer.Serialize(JsonSerializer.Serialize(layout));

        var ok = HomeLayoutSettingSerializer.TryDeserialize(stored, out var deserialized);

        ok.Should().BeTrue();
        deserialized.Should().NotBeNull();
        deserialized!.Rows.Should().ContainSingle();
        deserialized.Rows[0].LibraryIds.Should().BeEquivalentTo([LibraryA, LibraryB]);
    }

    [Test]
    public void TryDeserialize_ShouldReadSingleEncodedJson_WhenValueIsObject()
    {
        var layout = new HomeLayoutDto
        {
            Rows = [CreateRow("TV")]
        };

        var stored = JsonSerializer.Serialize(layout);

        var ok = HomeLayoutSettingSerializer.TryDeserialize(stored, out var deserialized);

        ok.Should().BeTrue();
        deserialized!.Rows.Should().ContainSingle();
        deserialized.Rows[0].Title.Should().Be("TV");
    }

    [Test]
    public void TryDeserialize_ShouldReturnFalse_WhenValueIsCorrupt()
    {
        var ok = HomeLayoutSettingSerializer.TryDeserialize("{not-json", out var layout);

        ok.Should().BeFalse();
        layout.Should().BeNull();
    }

    [Test]
    public void Serialize_ShouldRoundTripThroughStringSettingUnwrap()
    {
        var layout = new HomeLayoutDto
        {
            Rows = [CreateRow("Music", [LibraryA])]
        };

        var stored = HomeLayoutSettingSerializer.Serialize(layout);

        // Mimic UserSettingsService.GetAsync<string>
        var asStringSetting = JsonSerializer.Deserialize<string>(stored);
        asStringSetting.Should().NotBeNull();

        var fromGetAsync = JsonSerializer.Deserialize<HomeLayoutDto>(asStringSetting!);
        fromGetAsync.Should().NotBeNull();
        fromGetAsync!.Rows[0].Title.Should().Be("Music");

        HomeLayoutSettingSerializer.TryDeserialize(stored, out var fromRaw).Should().BeTrue();
        fromRaw!.Rows[0].LibraryIds.Should().BeEquivalentTo([LibraryA]);
    }

    private static HomeRowConfigDto CreateRow(string title, IReadOnlyList<Guid>? libraryIds = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            DisplayType = HomeRowDisplayType.Carousel,
            LibraryIds = libraryIds,
            PageSize = 20,
            ContinueWatching = false,
            IsVisible = true,
            Order = 0
        };
}