using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.SmartPlaylists.Commands.CreateSmartPlaylist;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record CreateSmartPlaylistCommand : IRequest<Guid>
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required MediaType MediaType { get; init; }
    public SmartPlaylistMatchCondition MatchCondition { get; init; }
    public IReadOnlyList<SmartPlaylistRuleCommand> Rules { get; init; } = [];
    public int? Limit { get; init; }
    public SmartPlaylistOrderBy OrderBy { get; init; } = SmartPlaylistOrderBy.DateAdded;
    public bool OrderDescending { get; init; } = true;
}

public record SmartPlaylistRuleCommand
{
    public SmartPlaylistField Field { get; init; }
    public SmartPlaylistOperator Operator { get; init; }
    public string? Value { get; init; }
}

public class CreateSmartPlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<CreateSmartPlaylistCommand, Guid>
{
    public async Task<Guid> Handle(CreateSmartPlaylistCommand request, CancellationToken cancellationToken)
    {
        var entity = new SmartPlaylist
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            UserId = currentUser.Id!.Value,
            MediaType = request.MediaType,
            MatchCondition = request.MatchCondition,
            Rules = request.Rules.Select(r => new SmartPlaylistRule
            {
                Field = r.Field,
                Operator = r.Operator,
                Value = r.Value
            }).ToList(),
            Limit = request.Limit,
            OrderBy = request.OrderBy,
            OrderDescending = request.OrderDescending
        };

        entity.AddDomainEvent(new SmartPlaylistCreatedEvent(entity));
        context.Playlists.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
