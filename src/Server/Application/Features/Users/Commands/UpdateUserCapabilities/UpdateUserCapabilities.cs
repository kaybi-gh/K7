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

public class UpdateUserCapabilitiesCommandHandler : IRequestHandler<UpdateUserCapabilitiesCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateUserCapabilitiesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateUserCapabilitiesCommand request, CancellationToken cancellationToken)
    {
        var domainUser = await _context.Users
            .Include(u => u.CapabilityOverrides)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, domainUser);

        domainUser.CapabilityOverrides.Clear();

        foreach (var dto in request.Overrides)
        {
            domainUser.CapabilityOverrides.Add(new UserCapabilityOverride
            {
                Id = Guid.NewGuid(),
                UserId = domainUser.Id,
                Capability = dto.Capability,
                Enabled = dto.Enabled
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
