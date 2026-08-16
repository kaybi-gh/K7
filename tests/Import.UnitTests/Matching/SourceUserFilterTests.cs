using K7.Import.Matching;
using K7.Import.Models;

namespace K7.Import.UnitTests.Matching;

[TestFixture]
public class SourceUserFilterTests
{
    private static readonly SourceUser Owner = new()
    {
        Id = "owner",
        Name = "kervichebastien@yahoo.fr"
    };

    private static readonly SourceUser Charlotte = new()
    {
        Id = "42",
        Name = "Charlotte"
    };

    private static readonly SourceUser TracearrUser = new()
    {
        Id = "abc",
        Name = "Kaybi",
        Detail = "Plex"
    };

    private static readonly IReadOnlyList<SourceUser> AllUsers = [Owner, Charlotte, TracearrUser];

    [Test]
    public void Apply_ShouldKeepAll_WhenFiltersAreEmpty()
    {
        var result = SourceUserFilter.Apply(AllUsers, []);

        result.IsActive.Should().BeFalse();
        result.Kept.Should().Equal(AllUsers);
        result.Excluded.Should().BeEmpty();
        result.UnmatchedFilters.Should().BeEmpty();
    }

    [Test]
    public void Apply_ShouldIgnoreBlankFilters()
    {
        var result = SourceUserFilter.Apply(AllUsers, ["", "  "]);

        result.IsActive.Should().BeFalse();
        result.Kept.Should().Equal(AllUsers);
    }

    [Test]
    public void Apply_ShouldKeepUser_WhenFilterMatchesRemoteName()
    {
        var result = SourceUserFilter.Apply(AllUsers, ["charlotte"]);

        result.IsActive.Should().BeTrue();
        result.Kept.Should().Equal(Charlotte);
        result.Excluded.Should().Equal(Owner, TracearrUser);
        result.UnmatchedFilters.Should().BeEmpty();
    }

    [Test]
    public void Apply_ShouldKeepUser_WhenFilterMatchesRemoteId()
    {
        var result = SourceUserFilter.Apply(AllUsers, ["owner"]);

        result.Kept.Should().Equal(Owner);
        result.UnmatchedFilters.Should().BeEmpty();
    }

    [Test]
    public void Apply_ShouldKeepUser_WhenFilterMatchesDisplayName()
    {
        var result = SourceUserFilter.Apply(AllUsers, ["Kaybi (Plex)"]);

        result.Kept.Should().Equal(TracearrUser);
        result.UnmatchedFilters.Should().BeEmpty();
    }

    [Test]
    public void Apply_ShouldKeepMultipleUsers_WhenSeveralFiltersMatch()
    {
        var result = SourceUserFilter.Apply(AllUsers, ["42", "kervichebastien@yahoo.fr"]);

        result.Kept.Should().Equal(Owner, Charlotte);
        result.Excluded.Should().Equal(TracearrUser);
    }

    [Test]
    public void Apply_ShouldReportUnmatchedFilter_WhenNameIsUnknown()
    {
        var result = SourceUserFilter.Apply(AllUsers, ["Charlotte", "Peter"]);

        result.Kept.Should().Equal(Charlotte);
        result.UnmatchedFilters.Should().Equal("Peter");
    }

    [Test]
    public void Apply_ShouldKeepNone_WhenNoFilterMatches()
    {
        var result = SourceUserFilter.Apply(AllUsers, ["Peter"]);

        result.Kept.Should().BeEmpty();
        result.Excluded.Should().Equal(AllUsers);
        result.UnmatchedFilters.Should().Equal("Peter");
    }
}
