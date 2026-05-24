using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Restrictions;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Features.Restrictions.Commands.CreateContentRestrictionProfile;

[Authorize(Roles = Roles.Administrator)]
public record CreateContentRestrictionProfileCommand : IRequest<Guid>
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required RuleGroupDto RuleFilter { get; init; }
}

public class CreateContentRestrictionProfileCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateContentRestrictionProfileCommand, Guid>
{
    public async Task<Guid> Handle(CreateContentRestrictionProfileCommand request, CancellationToken cancellationToken)
    {
        var entity = new ContentRestrictionProfile
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            RuleFilter = request.RuleFilter.ToRuleGroup()
        };

        context.ContentRestrictionProfiles.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
