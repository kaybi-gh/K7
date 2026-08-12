using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Shared;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class LocalUserServiceTests
{
    private IDeviceStorageService _storage = null!;
    private Dictionary<string, object?> _store = null!;
    private LocalUserService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new Dictionary<string, object?>(StringComparer.Ordinal);
        _storage = Substitute.For<IDeviceStorageService>();

        _storage.Get(Arg.Any<PreferenceKey<string>>())
            .Returns(ci =>
            {
                var key = ci.Arg<PreferenceKey<string>>();
                return _store.TryGetValue(key.Name, out var value) ? value as string : null;
            });

        _storage.Get(Arg.Any<PreferenceKey<bool>>())
            .Returns(ci =>
            {
                var key = ci.Arg<PreferenceKey<bool>>();
                return _store.TryGetValue(key.Name, out var value) && value is bool b && b;
            });

        _storage.When(s => s.Set(Arg.Any<PreferenceKey<string>>(), Arg.Any<string>()))
            .Do(ci => _store[ci.Arg<PreferenceKey<string>>().Name] = ci.Arg<string>());

        _storage.When(s => s.Set(Arg.Any<PreferenceKey<bool>>(), Arg.Any<bool>()))
            .Do(ci => _store[ci.Arg<PreferenceKey<bool>>().Name] = ci.Arg<bool>());

        _storage.When(s => s.Remove(Arg.Any<PreferenceKey<string>>()))
            .Do(ci => _store.Remove(ci.Arg<PreferenceKey<string>>().Name));

        _sut = new LocalUserService(_storage);
    }

    [Test]
    public void ClearRefreshToken_ShouldKeepProfile_AndEmptyToken()
    {
        var user = CreateUser("id-1", "kaybi", "rt-old");
        _sut.SaveOrUpdate(user);

        _sut.ClearRefreshToken("id-1");

        var stored = _sut.GetAll().Should().ContainSingle().Subject;
        stored.IdentityUserId.Should().Be("id-1");
        stored.UserName.Should().Be("kaybi");
        stored.RefreshToken.Should().BeEmpty();
    }

    [Test]
    public void ClearRefreshToken_ShouldNoOp_WhenUserMissing()
    {
        _sut.ClearRefreshToken("missing");
        _sut.GetAll().Should().BeEmpty();
    }

    [Test]
    public void UpdateRefreshToken_ShouldReplaceToken_WhenUserExists()
    {
        _sut.SaveOrUpdate(CreateUser("id-1", "kaybi", "rt-old"));

        _sut.UpdateRefreshToken("id-1", "rt-new");

        _sut.GetAll().Single().RefreshToken.Should().Be("rt-new");
    }

    [Test]
    public void UpdateRefreshToken_ShouldNoOp_WhenTokenEmpty()
    {
        _sut.SaveOrUpdate(CreateUser("id-1", "kaybi", "rt-old"));

        _sut.UpdateRefreshToken("id-1", "");

        _sut.GetAll().Single().RefreshToken.Should().Be("rt-old");
    }

    [Test]
    public void Remove_ShouldDeleteProfile()
    {
        _sut.SaveOrUpdate(CreateUser("id-1", "kaybi", "rt-old"));

        _sut.Remove("id-1");

        _sut.GetAll().Should().BeEmpty();
    }

    [Test]
    public void SaveOrUpdate_ShouldRoundTripJsonInStorage()
    {
        _sut.SaveOrUpdate(CreateUser("id-1", "kaybi", "rt-1"));

        var json = _store[PreferenceKeys.LOCAL_USERS.Name] as string;
        json.Should().NotBeNullOrEmpty();
        var users = JsonSerializer.Deserialize<List<LocalUser>>(json!);
        users.Should().ContainSingle(u => u.IdentityUserId == "id-1" && u.RefreshToken == "rt-1");
    }

    private static LocalUser CreateUser(string identityUserId, string userName, string refreshToken) =>
        new()
        {
            IdentityUserId = identityUserId,
            UserName = userName,
            RefreshToken = refreshToken
        };
}
