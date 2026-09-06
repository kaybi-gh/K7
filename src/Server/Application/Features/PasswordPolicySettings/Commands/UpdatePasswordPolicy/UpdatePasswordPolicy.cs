using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using K7.Shared.Security;

namespace K7.Server.Application.Features.PasswordPolicySettings.Commands.UpdatePasswordPolicy;

[Authorize(Roles = Roles.Administrator)]
public record UpdatePasswordPolicyCommand : IRequest<PasswordPolicyDto>
{
    public required PasswordPolicyDto Policy { get; init; }
}

public class UpdatePasswordPolicyCommandHandler(IPasswordPolicyService passwordPolicy)
    : IRequestHandler<UpdatePasswordPolicyCommand, PasswordPolicyDto>
{
    public async Task<PasswordPolicyDto> Handle(UpdatePasswordPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = PasswordPolicy.Normalize(request.Policy);
        await passwordPolicy.UpdateAsync(policy, cancellationToken);
        return policy;
    }
}
