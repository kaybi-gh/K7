using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MediaBrowseFilterPresetsTests
{
    [Test]
    public void SetSearchFieldValue_ShouldReplaceExistingActorRule()
    {
        var filter = MediaBrowseFilterPresets.SetSearchFieldValue(MediaBrowseFilterPresets.Empty, nameof(SmartPlaylistField.ActorName), "DiCaprio");

        MediaBrowseFilterPresets.GetSearchFieldValue(filter, nameof(SmartPlaylistField.ActorName)).Should().Be("DiCaprio");
    }

    [Test]
    public void ToggleContentRating_ShouldAddAndRemoveRating()
    {
        var withRating = MediaBrowseFilterPresets.ToggleContentRating(MediaBrowseFilterPresets.Empty, "PG-13");
        MediaBrowseFilterPresets.GetSelectedContentRatings(withRating).Should().BeEquivalentTo(["PG-13"]);

        var cleared = MediaBrowseFilterPresets.ToggleContentRating(withRating, "PG-13");
        MediaBrowseFilterPresets.GetSelectedContentRatings(cleared).Should().BeEmpty();
    }

    [Test]
    public void WithPreset_ShouldPreserveQuickMetadataFilters()
    {
        var filter = MediaBrowseFilterPresets.SetSearchFieldValue(MediaBrowseFilterPresets.Empty, "Studio", "Warner Bros.");
        filter = MediaBrowseFilterPresets.ToggleGenre(filter, "Action");

        var next = MediaBrowseFilterPresets.WithPreset(filter, MediaBrowseFilterPresets.Unwatched);

        MediaBrowseFilterPresets.IsUnwatched(next).Should().BeFalse();
        next.Items.OfType<ConditionRuleItemDto>()
            .Should()
            .ContainSingle(rule => rule.Field == nameof(SmartPlaylistField.IsCompleted) && rule.Value == "false");
        MediaBrowseFilterPresets.GetSearchFieldValue(next, "Studio").Should().Be("Warner Bros.");
        MediaBrowseFilterPresets.GetSelectedGenres(next).Should().BeEquivalentTo(["Action"]);
    }
}
