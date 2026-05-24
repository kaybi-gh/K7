using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Features.SmartPlaylists.Commands.UpdateSmartPlaylist;

public record UpdateSmartPlaylistCommand : IRequest
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required MediaType MediaType { get; init; }
    public RuleGroupDto RuleFilter { get; init; } = new() { MatchCondition = RuleMatchCondition.All, Items = [] };
    public int? Limit { get; init; }
    public SmartPlaylistOrderBy OrderBy { get; init; }
    public bool OrderDescending { get; init; }
}

public class UpdateSmartPlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<UpdateSmartPlaylistCommand>
{
    public async Task Handle(UpdateSmartPlaylistCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Playlists.OfType<SmartPlaylist>()
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.MediaType = request.MediaType;
        entity.RuleFilter = request.RuleFilter.ToRuleGroup();
        entity.Limit = request.Limit;
        entity.OrderBy = request.OrderBy;
        entity.OrderDescending = request.OrderDescending;

        entity.AddDomainEvent(new SmartPlaylistUpdatedEvent(entity));
        await context.SaveChangesAsync(cancellationToken);
    }
}
