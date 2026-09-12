using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Events;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.ClientAppPasswords.Commands.CreateClientAppPassword;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record CreateClientAppPasswordCommand : IRequest<CreateClientAppPasswordResponse>
{
    public required string Name { get; init; }
}

public class CreateClientAppPasswordCommandHandler(
    IApplicationDbContext context,
    IClientAppPasswordService clientAppPasswordService,
    IUser user,
    IIdentityService identityService)
    : IRequestHandler<CreateClientAppPasswordCommand, CreateClientAppPasswordResponse>
{
    public async Task<CreateClientAppPasswordResponse> Handle(
        CreateClientAppPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var (password, hash) = clientAppPasswordService.GeneratePassword();
        var userId = user.Id!.Value;
        var userName = user.IdentityId is not null
            ? await identityService.GetUserNameAsync(user.IdentityId)
            : null;

        var entity = new ClientAppPassword
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            PasswordHash = hash,
            UserId = userId
        };

        entity.AddDomainEvent(new ClientAppPasswordCreatedEvent(entity.Id, entity.Name, userId, userName));

        context.ClientAppPasswords.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return new CreateClientAppPasswordResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            Password = password
        };
    }
}
