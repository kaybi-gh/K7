using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared;
using K7.Shared.Dtos.SharedProfiles;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class SharedProfileLocalCacheTests
{
    private IDeviceStorageService _storage = null!;
    private Dictionary<string, object?> _store = null!;
    private ISharedProfileApi _api = null!;
    private IConnectivityService _connectivity = null!;
    private SharedProfileLocalCache _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new Dictionary<string, object?>(StringComparer.Ordinal);
        _storage = Substitute.For<IDeviceStorageService>();
        _api = Substitute.For<ISharedProfileApi>();
        _connectivity = Substitute.For<IConnectivityService>();
        _connectivity.IsOnline.Returns(true);

        _storage.Get(Arg.Any<PreferenceKey<string>>())
            .Returns(ci =>
            {
                var key = ci.Arg<PreferenceKey<string>>();
                return _store.TryGetValue(key.Name, out var value) ? value as string : null;
            });

        _storage.When(s => s.Set(Arg.Any<PreferenceKey<string>>(), Arg.Any<string>()))
            .Do(ci => _store[ci.Arg<PreferenceKey<string>>().Name] = ci.Arg<string>());

        _sut = new SharedProfileLocalCache(_storage, _api, _connectivity);
    }

    [Test]
    public void UpdateCache_ShouldKeepExistingGroups_WhenRefreshReturnsEmpty()
    {
        var familyId = Guid.NewGuid();
        _sut.UpdateCache([CreateGroup(familyId, "Family")]);

        _sut.UpdateCache([]);

        var cached = _sut.GetCached();
        cached.Should().ContainSingle();
        cached[0].Id.Should().Be(familyId);
        cached[0].Name.Should().Be("Family");
    }

    [Test]
    public void UpdateCache_ShouldUpsertMatchingId_AndKeepOtherGroups()
    {
        var familyId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        _sut.UpdateCache([CreateGroup(familyId, "Family"), CreateGroup(otherId, "Other")]);

        _sut.UpdateCache([CreateGroup(familyId, "Family renamed")]);

        var cached = _sut.GetCached();
        cached.Should().HaveCount(2);
        cached.Should().Contain(g => g.Id == familyId && g.Name == "Family renamed");
        cached.Should().Contain(g => g.Id == otherId && g.Name == "Other");
    }

    [Test]
    public async Task RefreshAsync_ShouldKeepCachedGroups_WhenApiReturnsEmpty()
    {
        var familyId = Guid.NewGuid();
        _sut.UpdateCache([CreateGroup(familyId, "Family")]);
        _api.GetSharedProfilesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SharedProfileDto>>([]));

        await _sut.RefreshAsync();

        _sut.FindById(familyId).Should().NotBeNull();
        _sut.FindById(familyId)!.Name.Should().Be("Family");
    }

    [Test]
    public async Task RefreshAsync_ShouldNotCallApi_WhenOffline()
    {
        _connectivity.IsOnline.Returns(false);

        await _sut.RefreshAsync();

        await _api.DidNotReceive().GetSharedProfilesAsync(Arg.Any<CancellationToken>());
    }

    private static SharedProfileDto CreateGroup(Guid id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            HostUserId = Guid.NewGuid(),
            HasPin = false,
            Members = []
        };
}
