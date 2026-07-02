using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class RuleFieldCatalogTests
{
    [Test]
    public void GetDescriptors_Movie_ShouldExposeTypedInputs()
    {
        var descriptors = RuleFieldCatalog.GetDescriptors(MediaType.Movie);
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
        var descriptors = RuleFieldCatalog.GetDescriptors(MediaType.Serie);
        descriptors.Select(d => d.FieldName).Should().Contain(nameof(SmartPlaylistField.ActorName));
    }

    [Test]
    public void GetDescriptors_Movie_OriginalLanguage_ShouldNotAllowContains()
    {
        var descriptor = RuleFieldCatalog
            .GetDescriptors(MediaType.Movie)
            .Single(d => d.FieldName == nameof(SmartPlaylistField.OriginalLanguage));

        descriptor.Operators.Should().NotContain(RuleOperator.Contains);
        descriptor.Operators.Should().Contain(RuleOperator.Equals);
    }

    [Test]
    public void GetDescriptors_Movie_ShouldNotIncludeMusicFields()
    {
        var descriptors = RuleFieldCatalog.GetDescriptors(MediaType.Movie);
        descriptors.Select(d => d.FieldName).Should().NotContain(nameof(SmartPlaylistField.AlbumTitle));
        descriptors.Select(d => d.FieldName).Should().NotContain(nameof(SmartPlaylistField.ArtistName));
    }

    [Test]
    public void GetAllDescriptors_ShouldIncludeReleaseYear()
    {
        RuleFieldCatalog.GetAllDescriptors()
            .Select(d => d.FieldName)
            .Should()
            .Contain(nameof(RestrictionField.ReleaseYear));
    }

    [Test]
    public void Sanitize_ShouldRemoveInvalidFields()
    {
        var filter = new RuleGroupDto
        {
            MatchCondition = RuleMatchCondition.All,
            Items =
            [
                new ConditionRuleItemDto
                {
                    Field = nameof(SmartPlaylistField.AlbumTitle),
                    Operator = RuleOperator.Equals,
                    Value = "Test"
                },
                new ConditionRuleItemDto
                {
                    Field = nameof(SmartPlaylistField.Title),
                    Operator = RuleOperator.Contains,
                    Value = "Movie"
                }
            ]
        };

        var allowed = RuleFieldCatalog.GetDescriptors(MediaType.Movie)
            .Select(d => d.FieldName)
            .ToHashSet(StringComparer.Ordinal);

        var sanitized = RuleFieldCatalog.Sanitize(filter, allowed);

        sanitized.Items.Should().HaveCount(1);
        ((ConditionRuleItemDto)sanitized.Items[0]).Field.Should().Be(nameof(SmartPlaylistField.Title));
    }
}
