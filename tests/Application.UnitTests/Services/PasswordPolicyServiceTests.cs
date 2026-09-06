using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.Database.Context.Services;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class PasswordPolicyServiceTests
{
    [Test]
    public async Task GetAsync_ShouldReturnDefaults_WhenSettingMissing()
    {
        var settings = Substitute.For<IServerSettingsService>();
        var identity = new IdentityOptions();
        settings.GetAsync(ServerSettingKeys.PasswordPolicy, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var service = new PasswordPolicyService(settings, Options.Create(identity));
        var policy = await service.GetAsync();

        policy.Should().BeEquivalentTo(PasswordPolicyDto.Defaults);
        identity.Password.RequiredLength.Should().Be(PasswordPolicyDto.DefaultRequiredLength);
        identity.Password.RequireNonAlphanumeric.Should().BeFalse();
    }

    [Test]
    public async Task GetAsync_ShouldApplyStoredPolicyToIdentity()
    {
        var settings = Substitute.For<IServerSettingsService>();
        var identity = new IdentityOptions();
        settings.GetAsync(ServerSettingKeys.PasswordPolicy, Arg.Any<CancellationToken>())
            .Returns("""{"RequiredLength":16,"RequiredUniqueChars":8,"RequireDigit":false,"RequireLowercase":true,"RequireUppercase":true,"RequireNonAlphanumeric":true}""");

        var service = new PasswordPolicyService(settings, Options.Create(identity));
        var policy = await service.GetAsync();

        policy.RequiredLength.Should().Be(16);
        policy.RequireDigit.Should().BeFalse();
        policy.RequireNonAlphanumeric.Should().BeTrue();
        identity.Password.RequiredLength.Should().Be(16);
        identity.Password.RequireDigit.Should().BeFalse();
        identity.Password.RequireNonAlphanumeric.Should().BeTrue();
    }

    [Test]
    public async Task UpdateAsync_ShouldPersistNormalizedPolicyAndApplyIdentity()
    {
        var settings = Substitute.For<IServerSettingsService>();
        var identity = new IdentityOptions();
        var service = new PasswordPolicyService(settings, Options.Create(identity));

        await service.UpdateAsync(new PasswordPolicyDto
        {
            RequiredLength = 400,
            RequiredUniqueChars = 12,
            RequireNonAlphanumeric = true
        });

        service.Current.RequiredLength.Should().Be(PasswordPolicyDto.MaxRequiredLength);
        identity.Password.RequiredLength.Should().Be(PasswordPolicyDto.MaxRequiredLength);
        identity.Password.RequiredUniqueChars.Should().Be(12);
        identity.Password.RequireNonAlphanumeric.Should().BeTrue();

        await settings.Received(1).SetAsync(
            ServerSettingKeys.PasswordPolicy,
            Arg.Is<string>(json => json.Contains($"\"RequiredLength\":{PasswordPolicyDto.MaxRequiredLength}")),
            Arg.Any<CancellationToken>());
    }
}
