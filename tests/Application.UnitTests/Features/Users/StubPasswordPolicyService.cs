using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Features.Users;

internal sealed class StubPasswordPolicyService(PasswordPolicyDto? policy = null) : IPasswordPolicyService
{
    public PasswordPolicyDto Current { get; } = policy ?? PasswordPolicyDto.Defaults;

    public Task<PasswordPolicyDto> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Current);

    public Task UpdateAsync(PasswordPolicyDto policy, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
