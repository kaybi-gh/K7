using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Users;

namespace K7.Server.Application.Features.Users.Commands.UpdateUserCapabilities;

[Authorize(Roles = Roles.Administrator)]
public record UpdateUserCapabilitiesCommand : IRequest
{
    public required Guid Id { get; init; }
    public required List<CapabilityOverrideDto> Overrides { get; init; }
}

public class UpdateUserCapabilitiesCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateUserCapabilitiesCommand>
{
    public async Task Handle(UpdateUserCapabilitiesCommand request, CancellationToken cancellationToken)
    {
        var existing = await context.UserCapabilityOverrides
            .Where(o => o.UserId == request.Id)
            .ToListAsync(cancellationToken);

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, user);

        context.UserCapabilityOverrides.RemoveRange(existing);

        context.UserCapabilityOverrides.AddRange(request.Overrides.Select(dto => new UserCapabilityOverride
        {
            Id = Guid.NewGuid(),
            UserId = request.Id,
            Capability = dto.Capability,
            Enabled = dto.Enabled
        }));

        await context.SaveChangesAsync(cancellationToken);
    }
}
