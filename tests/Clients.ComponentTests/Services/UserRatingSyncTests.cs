using K7.Clients.Shared.Services;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class UserRatingSyncTests
{
    [Test]
    public void Set_ShouldStoreNormalizedValueAndRaiseChanged()
    {
        var sync = new UserRatingSync([]);
        Guid? changedId = null;
        int? changedValue = 99;
        sync.Changed += (id, value) =>
        {
            changedId = id;
            changedValue = value;
        };

        var mediaId = Guid.NewGuid();
        sync.Set(mediaId, 8);

        sync.TryGet(mediaId, out var stored).Should().BeTrue();
        stored.Should().Be(8);
        changedId.Should().Be(mediaId);
        changedValue.Should().Be(8);
    }

    [Test]
    public void Set_ShouldTreatZeroAsCleared()
    {
        var sync = new UserRatingSync([]);
        var mediaId = Guid.NewGuid();
        sync.Set(mediaId, 0);

        sync.TryGet(mediaId, out var stored).Should().BeTrue();
        stored.Should().BeNull();
    }

    [Test]
    public void Set_ShouldNotRaiseChanged_WhenValueIsUnchanged()
    {
        var sync = new UserRatingSync([]);
        var mediaId = Guid.NewGuid();
        sync.Set(mediaId, 6);
        var raised = 0;
        sync.Changed += (_, _) => raised++;

        sync.Set(mediaId, 6);

        raised.Should().Be(0);
    }

    [Test]
    public void Clear_ShouldDropCachedRatings()
    {
        var sync = new UserRatingSync([]);
        var mediaId = Guid.NewGuid();
        sync.Set(mediaId, 4);

        sync.Clear();

        sync.TryGet(mediaId, out _).Should().BeFalse();
    }

    [Test]
    public void TryGet_ShouldReturnFalse_WhenMediaWasNeverRated()
    {
        var sync = new UserRatingSync([]);

        sync.TryGet(Guid.NewGuid(), out var value).Should().BeFalse();
        value.Should().BeNull();
    }
}
