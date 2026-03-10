using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.SmartPlaylists.Commands.UpdateSmartPlaylist;

public record UpdateSmartPlaylistCommand : IRequest
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public MediaType? MediaType { get; init; }
    public SmartPlaylistMatchCondition MatchCondition { get; init; }
    public IReadOnlyList<SmartPlaylistRuleCommand> Rules { get; init; } = [];
    public int? Limit { get; init; }
    public SmartPlaylistOrderBy OrderBy { get; init; }
    public bool OrderDescending { get; init; }
}

public record SmartPlaylistRuleCommand
{
    public SmartPlaylistField Field { get; init; }
    public SmartPlaylistOperator Operator { get; init; }
    public string? Value { get; init; }
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
        entity.MatchCondition = request.MatchCondition;
        entity.Rules = request.Rules.Select(r => new SmartPlaylistRule
        {
            Field = r.Field,
            Operator = r.Operator,
            Value = r.Value
        }).ToList();
        entity.Limit = request.Limit;
        entity.OrderBy = request.OrderBy;
        entity.OrderDescending = request.OrderDescending;

        entity.AddDomainEvent(new SmartPlaylistUpdatedEvent(entity));
        await context.SaveChangesAsync(cancellationToken);
    }
}
