using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MediaBrowseFilterFieldsTests
{
    [Test]
    public void GetDescriptors_Movie_ShouldExposeTypedInputs()
    {
        var descriptors = MediaBrowseFilterFields.GetDescriptors(MediaType.Movie);
        var byName = descriptors.ToDictionary(d => d.FieldName);

        byName[nameof(SmartPlaylistField.IsCompleted)].ValueType.Should().Be(RuleFieldValueType.Boolean);
        byName[nameof(SmartPlaylistField.IsCompleted)].Options.Should().NotBeNullOrEmpty();

        byName[nameof(SmartPlaylistField.OriginalLanguage)].ValueType.Should().Be(RuleFieldValueType.Language);
        byName[nameof(SmartPlaylistField.ActorName)].ValueType.Should().Be(RuleFieldValueType.Search);
        byName["Studio"].ValueType.Should().Be(RuleFieldValueType.Search);
        byName[nameof(SmartPlaylistField.Year)].ValueType.Should().Be(RuleFieldValueType.Number);
    }

    [Test]
    public void GetDescriptors_Serie_ShouldIncludeActorName()
    {
        var descriptors = MediaBrowseFilterFields.GetDescriptors(MediaType.Serie);
        descriptors.Select(d => d.FieldName).Should().Contain(nameof(SmartPlaylistField.ActorName));
    }

    [Test]
    public void GetDescriptors_Movie_OriginalLanguage_ShouldNotAllowContains()
    {
        var descriptor = MediaBrowseFilterFields
            .GetDescriptors(MediaType.Movie)
            .Single(d => d.FieldName == nameof(SmartPlaylistField.OriginalLanguage));

        descriptor.Operators.Should().NotContain(RuleOperator.Contains);
        descriptor.Operators.Should().Contain(RuleOperator.Equals);
    }
}
