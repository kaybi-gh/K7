using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Features.DynamicPlaylists.Commands.CreateDynamicPlaylist;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record CreateDynamicPlaylistCommand : IRequest<Guid>
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required MediaType MediaType { get; init; }
    public RuleGroupDto RuleFilter { get; init; } = new() { MatchCondition = RuleMatchCondition.All, Items = [] };
    public int? Limit { get; init; }
    public DynamicPlaylistOrderBy OrderBy { get; init; } = DynamicPlaylistOrderBy.DateAdded;
    public bool OrderDescending { get; init; } = true;
}

public class CreateDynamicPlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<CreateDynamicPlaylistCommand, Guid>
{
    public async Task<Guid> Handle(CreateDynamicPlaylistCommand request, CancellationToken cancellationToken)
    {
        var entity = new DynamicPlaylist
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            UserId = currentUser.Id!.Value,
            MediaType = request.MediaType,
            RuleFilter = request.RuleFilter.ToRuleGroup(),
            Limit = request.Limit,
            OrderBy = request.OrderBy,
            OrderDescending = request.OrderDescending
        };

        entity.AddDomainEvent(new DynamicPlaylistCreatedEvent(entity));
        context.Playlists.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
