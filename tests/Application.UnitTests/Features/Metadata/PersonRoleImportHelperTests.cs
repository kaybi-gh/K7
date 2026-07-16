using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

namespace K7.Server.Application.UnitTests.Features.Metadata;

[TestFixture]
public class PersonRoleImportHelperTests
{
    [Test]
    public void DedupByTmdbCreditId_ShouldRemoveDuplicateSameTypeAndTmdbId()
    {
        var roles = new List<BasePersonRole>
        {
            CreateActor("credit-1", "A"),
            CreateActor("credit-1", "B"),
            CreateActor("credit-2", "C")
        };

        PersonRoleImportHelper.DedupByTmdbCreditId(roles);

        roles.Should().HaveCount(2);
        roles.Select(r => r.ExternalIds.Single(e => e.ProviderName == "tmdb").Value)
            .Should().BeEquivalentTo("credit-1", "credit-2");
    }

    [Test]
    public void DedupByTmdbCreditId_ShouldKeepSameTmdbIdAcrossDifferentRoleTypes()
    {
        var actor = CreateActor("credit-1", "Actor");
        var crew = new CrewMember
        {
            Person = new Person { Name = "Director" },
            Job = "Director"
        };
        crew.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = "credit-1" });

        var roles = new List<BasePersonRole> { actor, crew };

        PersonRoleImportHelper.DedupByTmdbCreditId(roles);

        roles.Should().HaveCount(2);
    }

    [Test]
    public void DedupByTmdbCreditId_ShouldIgnoreRolesWithoutTmdbId()
    {
        var withTmdb = CreateActor("credit-1", "A");
        var without = new Actor
        {
            Person = new Person { Name = "No Id" },
            CharacterName = "X"
        };
        without.ExternalIds.Add(new ExternalId { ProviderName = "imdb", Value = "nm1" });

        var roles = new List<BasePersonRole> { withTmdb, without, CreateActor("credit-1", "Dup") };

        PersonRoleImportHelper.DedupByTmdbCreditId(roles);

        roles.Should().HaveCount(2);
        roles.Should().Contain(without);
    }

    private static Actor CreateActor(string tmdbCreditId, string name)
    {
        var actor = new Actor
        {
            Person = new Person { Name = name },
            CharacterName = name
        };
        actor.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = tmdbCreditId });
        return actor;
    }
}
