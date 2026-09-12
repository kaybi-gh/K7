using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Shared.Dtos.Users;
using K7.Shared.Security;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.CreateUser;

[Authorize(Roles = Roles.Administrator)]
public record CreateUserCommand : IRequest<UserDto>
{
    public required string Username { get; init; }
    public required string Role { get; init; }
    public string? Password { get; init; }
    public string? Email { get; init; }
}

public class CreateUserCommandHandler(
    IApplicationDbContext context,
    IIdentityService identityService,
    IPasswordPolicyService passwordPolicy)
    : IRequestHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var policy = await passwordPolicy.GetAsync(cancellationToken);
        var password = string.IsNullOrWhiteSpace(request.Password)
            ? PasswordPolicy.Generate(policy)
            : request.Password;
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var (result, identityUserId) = await identityService.CreateUserAsync(request.Username, password, email);

        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(MapIdentityFailure));
        }

        await identityService.SetRoleAsync(identityUserId, request.Role);

        var domainUser = new User
        {
            Id = Guid.NewGuid(),
            IdentityUserId = identityUserId,
            IsActive = true
        };

        domainUser.AddDomainEvent(new UserCreatedEvent(
            domainUser.Id,
            request.Username,
            email,
            request.Role,
            UserCreationOrigin.Admin));

        context.Users.Add(domainUser);
        await context.SaveChangesAsync(cancellationToken);

        var created = await context.Users
            .Include(u => u.CapabilityOverrides)
            .Include(u => u.LibraryExclusions)
            .Include(u => u.MediaExclusions)
            .FirstAsync(u => u.Id == domainUser.Id, cancellationToken);

        created.UserName = request.Username;
        created.Email = email;
        created.Role = request.Role;

        return created.ToUserDto();
    }

    private static ValidationFailure MapIdentityFailure(string error)
    {
        var property = error.Contains("Password", StringComparison.OrdinalIgnoreCase)
            ? "Password"
            : error.Contains("Email", StringComparison.OrdinalIgnoreCase)
                ? "Email"
                : "Username";

        return new ValidationFailure(property, error);
    }
}
