using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Users;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class GuardianCandidatesTests
{
    [Test]
    public void GetEligible_ShouldKeepOnlyLocalUsersAndAdministrators()
    {
        var child = User("child");
        var users = new[]
        {
            child,
            User("mother"),
            User("father", role: Roles.Administrator),
            User("guest", role: Roles.Guest),
            User("profile", hasIdentity: false),
            User("gone", isActive: false)
        };

        var eligible = GuardianCandidates.GetEligible(child, users);

        eligible.Select(u => u.UserName).Should().Equal("father", "mother");
    }

    [Test]
    public void GetEligible_ShouldKeepInactiveAccount_WhenItIsStillAGuardian()
    {
        var gone = User("gone", isActive: false);
        var child = User("child", guardians: [gone.Id]);

        var eligible = GuardianCandidates.GetEligible(child, [child, gone, User("mother")]);

        eligible.Select(u => u.UserName).Should().Equal("gone", "mother");
    }

    [Test]
    public void GetEligible_ShouldListCurrentGuardiansFirst_ThenByName()
    {
        var zelda = User("zelda");
        var child = User("child", guardians: [zelda.Id]);

        var eligible = GuardianCandidates.GetEligible(child, [User("bob"), child, zelda, User("Alice")]);

        eligible.Select(u => u.UserName).Should().Equal("zelda", "Alice", "bob");
    }

    [Test]
    public void GetVisible_ShouldListEveryCandidate_WhenUnderTheLimit()
    {
        var candidates = Users(3);

        var (items, hiddenCount) = GuardianCandidates.GetVisible(candidates, "", new HashSet<Guid>());

        items.Should().Equal(candidates);
        hiddenCount.Should().Be(0);
    }

    [Test]
    public void GetVisible_ShouldStopAtTheLimit_AndCountTheRest()
    {
        var candidates = Users(GuardianCandidates.MaxListed + 5);

        var (items, hiddenCount) = GuardianCandidates.GetVisible(candidates, null, new HashSet<Guid>());

        items.Should().Equal(candidates.Take(GuardianCandidates.MaxListed));
        hiddenCount.Should().Be(5);
    }

    [Test]
    public void GetVisible_ShouldAlwaysListPinnedAccounts_OnTopOfTheLimit()
    {
        var candidates = Users(6);
        var pinned = new HashSet<Guid> { candidates[5].Id };

        var (items, hiddenCount) = GuardianCandidates.GetVisible(candidates, "", pinned, maxListed: 2);

        items.Should().Equal(candidates[0], candidates[1], candidates[5]);
        hiddenCount.Should().Be(3);
    }

    [Test]
    public void GetVisible_ShouldReachAccountsBeyondTheLimit_WhenSearching()
    {
        var candidates = Users(6);

        var (items, hiddenCount) = GuardianCandidates.GetVisible(candidates, " USER5 ", new HashSet<Guid>(), maxListed: 2);

        items.Should().Equal(candidates[5]);
        hiddenCount.Should().Be(0);
    }

    [Test]
    public void GetVisible_ShouldSearchDisplayNameUserNameAndEmail()
    {
        var byDisplayName = User("a", displayName: "Marie Curie");
        var byUserName = User("marie42");
        var byEmail = User("b", email: "marie@example.com");
        var other = User("pierre");
        var candidates = new[] { byDisplayName, byUserName, byEmail, other };

        var (items, _) = GuardianCandidates.GetVisible(candidates, "marie", new HashSet<Guid>());

        items.Should().Equal(byDisplayName, byUserName, byEmail);
    }

    [Test]
    public void GetVisible_ShouldHidePinnedAccount_WhenItDoesNotMatchTheSearch()
    {
        var candidates = Users(3);
        var pinned = new HashSet<Guid> { candidates[0].Id };

        var (items, hiddenCount) = GuardianCandidates.GetVisible(candidates, "user2", pinned);

        items.Should().Equal(candidates[2]);
        hiddenCount.Should().Be(0);
    }

    [Test]
    public void GetName_ShouldPreferDisplayName_ThenUserName_ThenEmail()
    {
        GuardianCandidates.GetName(User("kay", displayName: "Kay", email: "kay@example.com")).Should().Be("Kay");
        GuardianCandidates.GetName(User("kay", email: "kay@example.com")).Should().Be("kay");
        GuardianCandidates.GetName(User(null, email: "kay@example.com")).Should().Be("kay@example.com");
    }

    private static List<UserDto> Users(int count) =>
        Enumerable.Range(0, count).Select(i => User($"user{i}")).ToList();

    private static UserDto User(
        string? userName,
        string role = Roles.User,
        bool isActive = true,
        bool hasIdentity = true,
        string? displayName = null,
        string? email = null,
        IReadOnlyList<Guid>? guardians = null) => new()
        {
            Id = Guid.NewGuid(),
            IdentityUserId = hasIdentity ? Guid.NewGuid().ToString() : null,
            UserName = userName,
            DisplayName = displayName,
            Email = email,
            Role = role,
            Created = DateTimeOffset.UnixEpoch,
            IsActive = isActive,
            IsGuest = role == Roles.Guest,
            HasPin = false,
            CapabilityOverrides = [],
            LibraryExclusions = [],
            MediaExclusions = [],
            GuardianUserIds = guardians ?? []
        };
}
