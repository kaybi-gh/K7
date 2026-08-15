using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.GeneralPreferences.Queries.GetEffectiveGeneralPreferences;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.GeneralPreferences.Queries;

[TestFixture]
public class GetEffectiveGeneralPreferencesQueryHandlerTests
{
    private IUserSettingsService _userSettings = null!;
    private IUser _currentUser = null!;
    private GetEffectiveGeneralPreferencesQueryHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _userSettings = Substitute.For<IUserSettingsService>();
        _currentUser = Substitute.For<IUser>();
        _handler = new GetEffectiveGeneralPreferencesQueryHandler(_userSettings, _currentUser);
    }

    [Test]
    public async Task Handle_ShouldReturnUserOverrides_WhenUserSettingExists()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        _currentUser.Id.Returns(userId);
        _userSettings.GetAsync(userId, UserSettingKeys.GeneralPreferences, Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new GeneralPreferencesDto
            {
                ExploreTapActions = { [groupId] = ExploreTapAction.Browse }
            }));

        var result = await _handler.Handle(new GetEffectiveGeneralPreferencesQuery(), CancellationToken.None);

        result.ResolveExploreTapAction(groupId, ExploreTapAction.Suggestions).Should().Be(ExploreTapAction.Browse);
    }

    [Test]
    public async Task Handle_ShouldReturnEmptyOverrides_WhenUserHasNoSetting()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        _currentUser.Id.Returns(userId);
        _userSettings.GetAsync(userId, UserSettingKeys.GeneralPreferences, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await _handler.Handle(new GetEffectiveGeneralPreferencesQuery(), CancellationToken.None);

        result.ExploreTapActions.Should().BeEmpty();
        result.ResolveExploreTapAction(groupId, ExploreTapAction.Suggestions).Should().Be(ExploreTapAction.Suggestions);
    }
}
