using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Services;

public class SupplementalSerieCastEnricherTests
{
    [Test]
    public void Enrich_ShouldMergeTmdbPersonData_WhenCharacterAndNameMatch()
    {
        var tvdbRoles = new List<BasePersonRole>
        {
            CreateTvdbActor("Rebecca Ferguson", "Juliette", "403245")
        };
        var tmdbRoles = new List<BasePersonRole>
        {
            CreateTmdbActor("Rebecca Ferguson", "Juliette Nichols", "12345", "nm1", birthday: new DateOnly(1983, 10, 19), birthPlace: "Stockholm")
        };

        var result = SupplementalSerieCastEnricher.Enrich(tvdbRoles, tmdbRoles);

        result.RolesToAppend.Should().BeEmpty();
        result.UnresolvedTvdbPeopleIds.Should().BeEmpty();

        var person = tvdbRoles[0].Person;
        person.Birthday.Should().Be(new DateOnly(1983, 10, 19));
        person.BirthPlace.Should().Be("Stockholm");
        person.ExternalIds.Should().Contain(e => e.ProviderName == "tvdb" && e.Value == "403245");
        person.ExternalIds.Should().Contain(e => e.ProviderName == "tmdb" && e.Value == "12345");
        person.ExternalIds.Should().Contain(e => e.ProviderName == "imdb" && e.Value == "nm1");
    }

    [Test]
    public void Enrich_ShouldMatchByUniqueCharacterName_WhenPersonNamesDifferSlightly()
    {
        var tvdbRoles = new List<BasePersonRole>
        {
            CreateTvdbActor("R. Ferguson", "Juliette", "1")
        };
        var tmdbRoles = new List<BasePersonRole>
        {
            CreateTmdbActor("Rebecca Ferguson", "Juliette", "99", null)
        };

        var result = SupplementalSerieCastEnricher.Enrich(tvdbRoles, tmdbRoles);

        result.UnresolvedTvdbPeopleIds.Should().BeEmpty();
        tvdbRoles[0].Person.ExternalIds.Should().Contain(e => e.ProviderName == "tmdb" && e.Value == "99");
    }

    [Test]
    public void Enrich_ShouldNotMatchAmbiguousCharacterNames()
    {
        var tvdbRoles = new List<BasePersonRole>
        {
            CreateTvdbActor("Actor A", "Doctor", "1")
        };
        var tmdbRoles = new List<BasePersonRole>
        {
            CreateTmdbActor("Person One", "Doctor", "10", null),
            CreateTmdbActor("Person Two", "Doctor", "11", null)
        };

        var result = SupplementalSerieCastEnricher.Enrich(tvdbRoles, tmdbRoles);

        result.UnresolvedTvdbPeopleIds.Should().ContainSingle().Which.Should().Be("1");
        tvdbRoles[0].Person.ExternalIds.Should().NotContain(e => e.ProviderName == "tmdb");
        result.RolesToAppend.Should().HaveCount(2);
    }

    [Test]
    public void Enrich_ShouldAppendUnmatchedTmdbCrew()
    {
        var tvdbRoles = new List<BasePersonRole>
        {
            CreateTvdbActor("Lead Actor", "Hero", "1")
        };
        var tmdbRoles = new List<BasePersonRole>
        {
            CreateTmdbActor("Lead Actor", "Hero", "50", null),
            CreateTmdbCrew("Graham Yost", "Writing", "Executive Producer", "200")
        };

        var result = SupplementalSerieCastEnricher.Enrich(tvdbRoles, tmdbRoles);

        result.RolesToAppend.Should().ContainSingle()
            .Which.Should().BeOfType<CrewMember>()
            .Which.Person.Name.Should().Be("Graham Yost");
        result.UnresolvedTvdbPeopleIds.Should().BeEmpty();
    }

    [Test]
    public void Enrich_ShouldReportUnresolvedTvdbIds_WhenNoSupplementalMatch()
    {
        var tvdbRoles = new List<BasePersonRole>
        {
            CreateTvdbActor("Only On Tvdb", "Extra", "777")
        };

        var result = SupplementalSerieCastEnricher.Enrich(tvdbRoles, []);

        result.UnresolvedTvdbPeopleIds.Should().ContainSingle().Which.Should().Be("777");
        result.RolesToAppend.Should().BeEmpty();
    }

    [Test]
    public void Enrich_ShouldMatchByExistingTmdbId()
    {
        var person = new Person
        {
            Name = "Known Person",
            ExternalIds =
            [
                new ExternalId { ProviderName = "tvdb", Value = "5" },
                new ExternalId { ProviderName = "tmdb", Value = "42" }
            ]
        };
        var tvdbRoles = new List<BasePersonRole>
        {
            new Actor { CharacterName = "Someone", Person = person }
        };
        var tmdbRoles = new List<BasePersonRole>
        {
            CreateTmdbActor("Known Person", "Someone Else", "42", null, biography: "Bio from TMDb")
        };

        SupplementalSerieCastEnricher.Enrich(tvdbRoles, tmdbRoles);

        person.Biography.Should().Be("Bio from TMDb");
    }

    [Test]
    public void Normalize_ShouldStripDiacriticsAndPunctuation()
    {
        SupplementalSerieCastEnricher.Normalize("José-María O'Neil")
            .Should().Be("jose maria oneil");
    }

    private static Actor CreateTvdbActor(string personName, string character, string tvdbPeopleId) =>
        new()
        {
            CharacterName = character,
            Person = new Person
            {
                Name = personName,
                ExternalIds = [new ExternalId { ProviderName = "tvdb", Value = tvdbPeopleId }]
            }
        };

    private static Actor CreateTmdbActor(
        string personName,
        string character,
        string tmdbId,
        string? imdbId,
        DateOnly? birthday = null,
        string? birthPlace = null,
        string? biography = null)
    {
        var person = new Person
        {
            Name = personName,
            Birthday = birthday,
            BirthPlace = birthPlace,
            Biography = biography,
            ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = tmdbId }]
        };
        if (imdbId is not null)
            person.ExternalIds.Add(new ExternalId { ProviderName = "imdb", Value = imdbId });

        return new Actor
        {
            CharacterName = character,
            Person = person,
            ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = $"credit-{tmdbId}" }]
        };
    }

    private static CrewMember CreateTmdbCrew(string personName, string department, string job, string tmdbId) =>
        new()
        {
            Department = department,
            Job = job,
            Person = new Person
            {
                Name = personName,
                ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = tmdbId }]
            },
            ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = $"credit-{tmdbId}" }]
        };
}

public class PersonMetadataMergeHelperTests
{
    [Test]
    public void MergeMissingPersonData_ShouldFillEmptyFieldsWithoutOverwriting()
    {
        var target = new Person
        {
            Name = "A",
            Biography = "Keep me",
            ExternalIds = [new ExternalId { ProviderName = "tvdb", Value = "1" }]
        };
        var source = new Person
        {
            Name = "A",
            Biography = "Ignore",
            Birthday = new DateOnly(1990, 1, 1),
            BirthPlace = "Paris",
            Gender = PersonGender.Female,
            ExternalIds =
            [
                new ExternalId { ProviderName = "tvdb", Value = "1" },
                new ExternalId { ProviderName = "tmdb", Value = "9" }
            ]
        };

        PersonMetadataMergeHelper.MergeMissingPersonData(target, source);

        target.Biography.Should().Be("Keep me");
        target.Birthday.Should().Be(new DateOnly(1990, 1, 1));
        target.BirthPlace.Should().Be("Paris");
        target.Gender.Should().Be(PersonGender.Female);
        target.ExternalIds.Should().Contain(e => e.ProviderName == "tmdb" && e.Value == "9");
        target.ExternalIds.Count(e => e.ProviderName == "tvdb").Should().Be(1);
    }

    [Test]
    public void NeedsProviderRefresh_ShouldBeTrue_WhenTmdbPresentButProfileThin()
    {
        var person = new Person
        {
            Name = "Thin",
            ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = "1" }]
        };

        PersonMetadataMergeHelper.NeedsProviderRefresh(person).Should().BeTrue();
    }

    [Test]
    public void NeedsProviderRefresh_ShouldBeFalse_WhenBiographyPresent()
    {
        var person = new Person
        {
            Name = "Rich",
            Biography = "Has bio",
            ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = "1" }]
        };

        PersonMetadataMergeHelper.NeedsProviderRefresh(person).Should().BeFalse();
    }
}
