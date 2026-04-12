using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Users;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Users.Commands.CreateUser;

[Authorize(Roles = Roles.Administrator)]
public record CreateUserCommand : IRequest<UserDto>
{
    public required string Username { get; init; }
    public required string Role { get; init; }
    public string? Password { get; init; }
}

public class CreateUserCommandHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var password = request.Password ?? Guid.NewGuid().ToString("N") + "A!1";
        var (result, identityUserId) = await identityService.CreateUserAsync(request.Username, password);

        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e =>
                new FluentValidation.Results.ValidationFailure("Username", e)).ToList());
        }

        await identityService.SetRoleAsync(identityUserId, request.Role);

        var domainUser = new User
        {
            IdentityUserId = identityUserId,
            IsActive = true
        };

        context.Users.Add(domainUser);
        await context.SaveChangesAsync(cancellationToken);

        var created = await context.Users
            .Include(u => u.CapabilityOverrides)
            .Include(u => u.LibraryExclusions)
            .Include(u => u.MediaExclusions)
            .FirstAsync(u => u.Id == domainUser.Id, cancellationToken);

        created.UserName = request.Username;
        created.Email = request.Username.Contains('@') ? request.Username : null;
        created.Role = request.Role;

        return created.ToUserDto();
    }
}
