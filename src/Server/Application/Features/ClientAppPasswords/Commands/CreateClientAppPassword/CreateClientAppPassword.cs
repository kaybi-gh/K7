using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
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
    IUser user)
    : IRequestHandler<CreateClientAppPasswordCommand, CreateClientAppPasswordResponse>
{
    public async Task<CreateClientAppPasswordResponse> Handle(
        CreateClientAppPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var (password, hash) = clientAppPasswordService.GeneratePassword();

        var entity = new ClientAppPassword
        {
            Name = request.Name,
            PasswordHash = hash,
            UserId = user.Id!.Value
        };

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
