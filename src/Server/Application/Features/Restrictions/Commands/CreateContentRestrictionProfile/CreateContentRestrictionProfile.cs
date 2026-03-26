using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Restrictions.Commands.CreateContentRestrictionProfile;

[Authorize(Roles = Roles.Administrator)]
public record CreateContentRestrictionProfileCommand : IRequest<Guid>
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public RestrictionMatchCondition MatchCondition { get; init; }
    public required IReadOnlyList<ContentRestrictionRuleCommand> Rules { get; init; }
}

public record ContentRestrictionRuleCommand
{
    public RestrictionField Field { get; init; }
    public RestrictionOperator Operator { get; init; }
    public string? Value { get; init; }
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
            MatchCondition = request.MatchCondition,
            Rules = request.Rules.Select(r => new ContentRestrictionRule
            {
                Field = r.Field,
                Operator = r.Operator,
                Value = r.Value
            }).ToList()
        };

        context.ContentRestrictionProfiles.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
