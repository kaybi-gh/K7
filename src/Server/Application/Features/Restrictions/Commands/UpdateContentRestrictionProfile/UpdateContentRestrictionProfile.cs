using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Features.Restrictions.Commands.UpdateContentRestrictionProfile;

[Authorize(Roles = Roles.Administrator)]
public record UpdateContentRestrictionProfileCommand : IRequest
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required RuleGroupDto RuleFilter { get; init; }
}

public class UpdateContentRestrictionProfileCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateContentRestrictionProfileCommand>
{
    public async Task Handle(UpdateContentRestrictionProfileCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.ContentRestrictionProfiles
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.RuleFilter = request.RuleFilter.ToRuleGroup();

        await context.SaveChangesAsync(cancellationToken);
    }
}
