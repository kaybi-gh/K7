using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Interfaces;

public interface IPasswordPolicyService
{
    PasswordPolicyDto Current { get; }

    Task<PasswordPolicyDto> GetAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(PasswordPolicyDto policy, CancellationToken cancellationToken = default);
}
