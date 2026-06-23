using K7.Clients.Shared.Helpers;
using K7.Shared.Dtos.Entities.PersonRoles;
using K7.Shared.Dtos.Entities.Persons;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class PersonRoleDisplayHelperTests
{
    private static readonly Guid PersonId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Test]
    public void GroupForCarousel_ShouldMergeActorAndCrewRolesForSamePerson()
    {
        var roles = new LitePersonRoleDto[]
        {
            new LiteActorDto
            {
                Id = Guid.NewGuid(),
                Order = 1,
                CharacterName = "Arthur Curry",
                Person = new LitePersonDto { Id = PersonId, Name = "Jason Momoa" }
            },
            new LiteCrewMemberDto
            {
                Id = Guid.NewGuid(),
                Order = null,
                Department = "Directing",
                Job = "Director",
                Person = new LitePersonDto { Id = PersonId, Name = "Jason Momoa" }
            }
        };

        var grouped = PersonRoleDisplayHelper.GroupForCarousel(roles);

        grouped.Should().ContainSingle();
        grouped[0].Key.Should().Be(PersonId);
        grouped[0].MergedSubtitle.Should().Be("Arthur Curry · Directing / Director");
    }

    [Test]
    public void GroupForCarousel_ShouldKeepDistinctPeopleSeparate()
    {
        var roles = new LitePersonRoleDto[]
        {
            new LiteActorDto
            {
                Id = Guid.NewGuid(),
                Order = 1,
                CharacterName = "Hero",
                Person = new LitePersonDto { Id = PersonId, Name = "Actor A" }
            },
            new LiteActorDto
            {
                Id = Guid.NewGuid(),
                Order = 2,
                CharacterName = "Villain",
                Person = new LitePersonDto { Id = Guid.NewGuid(), Name = "Actor B" }
            }
        };

        PersonRoleDisplayHelper.GroupForCarousel(roles).Should().HaveCount(2);
    }

    [Test]
    public void GroupForCarousel_ShouldUseFirstDisplayOrderRoleAsPrimary_EvenWhenCrewListedFirstInInput()
    {
        var roles = new LitePersonRoleDto[]
        {
            new LiteCrewMemberDto
            {
                Id = Guid.NewGuid(),
                Order = null,
                Department = "Writing",
                Job = "Story",
                Person = new LitePersonDto { Id = PersonId, Name = "Jason Momoa" }
            },
            new LiteActorDto
            {
                Id = Guid.NewGuid(),
                Order = 1,
                CharacterName = "Arthur Curry",
                Person = new LitePersonDto { Id = PersonId, Name = "Jason Momoa" }
            },
            new LiteActorDto
            {
                Id = Guid.NewGuid(),
                Order = 2,
                CharacterName = "Co-star",
                Person = new LitePersonDto { Id = Guid.NewGuid(), Name = "Other Actor" }
            }
        };

        var grouped = PersonRoleDisplayHelper.GroupForCarousel(roles);

        grouped.Should().HaveCount(2);
        grouped[0].Key.Should().Be(PersonId);
        grouped[0].PrimaryRole.Should().BeOfType<LiteActorDto>();
        ((LiteActorDto)grouped[0].PrimaryRole).CharacterName.Should().Be("Arthur Curry");
        grouped[0].MergedSubtitle.Should().Be("Arthur Curry · Writing / Story");
    }

    [Test]
    public void GroupForCarousel_ShouldOrderByFirstAppearanceInDisplayList()
    {
        var firstPersonId = Guid.NewGuid();
        var secondPersonId = Guid.NewGuid();
        var roles = new LitePersonRoleDto[]
        {
            new LiteActorDto
            {
                Id = Guid.NewGuid(),
                Order = 5,
                CharacterName = "Late",
                Person = new LitePersonDto { Id = secondPersonId, Name = "Late Actor" }
            },
            new LiteActorDto
            {
                Id = Guid.NewGuid(),
                Order = 1,
                CharacterName = "Early",
                Person = new LitePersonDto { Id = firstPersonId, Name = "Early Actor" }
            }
        };

        var grouped = PersonRoleDisplayHelper.GroupForCarousel(roles);

        grouped.Should().HaveCount(2);
        grouped[0].Key.Should().Be(firstPersonId);
        grouped[1].Key.Should().Be(secondPersonId);
    }
}
