using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Json;

namespace K7.Server.Application.Features.Federation.Services;

public interface IFederationSocialPolicyService
{
    Task<FederationSocialPolicyDto> GetAsync(CancellationToken cancellationToken = default);
    Task SetAsync(FederationSocialPolicyDto policy, CancellationToken cancellationToken = default);
}

public class FederationSocialPolicyService(IServerSettingsService serverSettings) : IFederationSocialPolicyService
{
    public async Task<FederationSocialPolicyDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var json = await serverSettings.GetAsync(ServerSettingKeys.FederationSocialPolicy, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return CreateDefault();

        return JsonSerializer.Deserialize<FederationSocialPolicyDto>(json, K7JsonSerializerOptions.CreateDefault()) ?? CreateDefault();
    }

    public async Task SetAsync(FederationSocialPolicyDto policy, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(policy, K7JsonSerializerOptions.CreateDefault());
        await serverSettings.SetAsync(ServerSettingKeys.FederationSocialPolicy, json, cancellationToken);
    }

    private static FederationSocialPolicyDto CreateDefault()
    {
        var policies = Enum.GetValues<FederationContentType>()
            .ToDictionary(
                t => t,
                _ => new FederationContentTypePolicyDto { Outbound = false, Inbound = false });

        return new FederationSocialPolicyDto
        {
            Enabled = false,
            Policies = policies
        };
    }
}
