using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Restrictions.Commands.UpdateContentRestrictionProfile;

[Authorize(Roles = Roles.Administrator)]
public record UpdateContentRestrictionProfileCommand : IRequest
{
    public required Guid Id { get; init; }
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
        entity.MatchCondition = request.MatchCondition;
        entity.Rules = request.Rules.Select(r => new ContentRestrictionRule
        {
            Field = r.Field,
            Operator = r.Operator,
            Value = r.Value
        }).ToList();

        await context.SaveChangesAsync(cancellationToken);
    }
}
