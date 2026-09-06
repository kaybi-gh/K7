using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
using K7.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.Database.Context.Services;

public sealed class PasswordPolicyService(
    IServerSettingsService serverSettings,
    IOptions<IdentityOptions> identityOptions) : IPasswordPolicyService
{
    public PasswordPolicyDto Current { get; private set; } = PasswordPolicyDto.Defaults;

    public async Task<PasswordPolicyDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var json = await serverSettings.GetAsync(ServerSettingKeys.PasswordPolicy, cancellationToken);
        Current = Parse(json);
        ApplyToIdentity(Current);
        return Current;
    }

    public async Task UpdateAsync(PasswordPolicyDto policy, CancellationToken cancellationToken = default)
    {
        Current = PasswordPolicy.Normalize(policy);
        await serverSettings.SetAsync(
            ServerSettingKeys.PasswordPolicy,
            JsonSerializer.Serialize(Current),
            cancellationToken);
        ApplyToIdentity(Current);
    }

    private void ApplyToIdentity(PasswordPolicyDto policy)
    {
        var options = identityOptions.Value.Password;
        options.RequiredLength = policy.RequiredLength;
        options.RequiredUniqueChars = policy.RequiredUniqueChars;
        options.RequireDigit = policy.RequireDigit;
        options.RequireLowercase = policy.RequireLowercase;
        options.RequireUppercase = policy.RequireUppercase;
        options.RequireNonAlphanumeric = policy.RequireNonAlphanumeric;
    }

    private static PasswordPolicyDto Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return PasswordPolicyDto.Defaults;

        return PasswordPolicy.Normalize(
            JsonSerializer.Deserialize<PasswordPolicyDto>(json) ?? PasswordPolicyDto.Defaults);
    }
}
