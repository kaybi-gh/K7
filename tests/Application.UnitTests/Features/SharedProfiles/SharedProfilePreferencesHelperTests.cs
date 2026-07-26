using System.Text.Json;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Entities.Settings;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.SharedProfiles;

[TestFixture]
public class SharedProfilePreferencesHelperTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public void ParsePreferences_ShouldUnwrapDoubleEncodedJson()
    {
        var dto = new SharedProfilePreferencesDto { BlockNewMembership = false };
        var inner = JsonSerializer.Serialize(dto);
        var stored = JsonSerializer.Serialize(inner);

        var parsed = SharedProfilePreferencesHelper.ParsePreferences(stored);

        parsed.BlockNewMembership.Should().BeFalse();
    }

    [Test]
    public void ParsePreferences_ShouldAcceptPlainDtoJson()
    {
        var json = JsonSerializer.Serialize(new SharedProfilePreferencesDto { BlockNewMembership = false });

        var parsed = SharedProfilePreferencesHelper.ParsePreferences(json);

        parsed.BlockNewMembership.Should().BeFalse();
    }

    [Test]
    public void ParsePreferences_ShouldDefaultToBlocked_WhenInvalid()
    {
        var parsed = SharedProfilePreferencesHelper.ParsePreferences("not-json");

        parsed.BlockNewMembership.Should().BeTrue();
    }

    [Test]
    public async Task GetUsersBlockingMembershipAsync_ShouldTreatMissingSettingAsBlocked()
    {
        var userId = Guid.NewGuid();

        var blocked = await SharedProfilePreferencesHelper.GetUsersBlockingMembershipAsync(
            _context, [userId], CancellationToken.None);

        blocked.Should().Contain(userId);
    }

    [Test]
    public async Task GetUsersBlockingMembershipAsync_ShouldRespectStoredAllowPreference()
    {
        var userId = Guid.NewGuid();
        var inner = JsonSerializer.Serialize(new SharedProfilePreferencesDto { BlockNewMembership = false });
        _context.UserSettings.Add(new UserSetting
        {
            UserId = userId,
            Key = UserSettingKeys.SharedProfilePreferences.Name,
            Value = JsonSerializer.Serialize(inner)
        });
        await _context.SaveChangesAsync();

        var blocked = await SharedProfilePreferencesHelper.GetUsersBlockingMembershipAsync(
            _context, [userId], CancellationToken.None);

        blocked.Should().BeEmpty();
    }

    [Test]
    public async Task GetUsersBlockingMembershipAsync_ShouldNotThrow_OnDoubleEncodedValues()
    {
        var userId = Guid.NewGuid();
        var inner = JsonSerializer.Serialize(new SharedProfilePreferencesDto { BlockNewMembership = true });
        _context.UserSettings.Add(new UserSetting
        {
            UserId = userId,
            Key = UserSettingKeys.SharedProfilePreferences.Name,
            Value = JsonSerializer.Serialize(inner)
        });
        await _context.SaveChangesAsync();

        var act = () => SharedProfilePreferencesHelper.GetUsersBlockingMembershipAsync(
            _context, [userId], CancellationToken.None);

        await act.Should().NotThrowAsync();
        var blocked = await act();
        blocked.Should().Contain(userId);
    }
}
