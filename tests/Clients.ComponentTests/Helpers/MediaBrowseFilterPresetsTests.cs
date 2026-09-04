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
        var filter = MediaBrowseFilterPresets.SetSearchFieldValue(MediaBrowseFilterPresets.Empty, nameof(DynamicPlaylistField.ActorName), "DiCaprio");

        MediaBrowseFilterPresets.GetSearchFieldValue(filter, nameof(DynamicPlaylistField.ActorName)).Should().Be("DiCaprio");
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
            .ContainSingle(rule => rule.Field == nameof(DynamicPlaylistField.IsCompleted) && rule.Value == "false");
        MediaBrowseFilterPresets.GetSearchFieldValue(next, "Studio").Should().Be("Warner Bros.");
        MediaBrowseFilterPresets.GetSelectedGenres(next).Should().BeEquivalentTo(["Action"]);
    }

    [Test]
    public void WithPreset_ShouldSwitchBetweenUnwatchedAndInProgress()
    {
        var unwatched = MediaBrowseFilterPresets.WithPreset(
            MediaBrowseFilterPresets.Empty,
            MediaBrowseFilterPresets.Unwatched);
        var inProgress = MediaBrowseFilterPresets.WithPreset(
            unwatched,
            MediaBrowseFilterPresets.InProgress);

        MediaBrowseFilterPresets.AreEquivalent(unwatched, inProgress).Should().BeFalse();
        MediaBrowseFilterPresets.IsInProgress(inProgress).Should().BeTrue();
        MediaBrowseFilterPresets.IsUnwatched(inProgress).Should().BeFalse();

        var backToUnwatched = MediaBrowseFilterPresets.WithPreset(
            inProgress,
            MediaBrowseFilterPresets.Unwatched);
        MediaBrowseFilterPresets.IsUnwatched(backToUnwatched).Should().BeTrue();
        MediaBrowseFilterPresets.IsInProgress(backToUnwatched).Should().BeFalse();
    }

    [Test]
    public void AreEquivalent_ShouldBeTrue_WhenWatchPresetIsUnchanged()
    {
        var first = MediaBrowseFilterPresets.WithPreset(
            MediaBrowseFilterPresets.Empty,
            MediaBrowseFilterPresets.Unwatched);
        var second = MediaBrowseFilterPresets.WithPreset(first, MediaBrowseFilterPresets.Unwatched);

        MediaBrowseFilterPresets.AreEquivalent(first, second).Should().BeTrue();
    }
}
