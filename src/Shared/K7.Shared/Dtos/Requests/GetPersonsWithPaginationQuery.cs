using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record GetPersonsWithPaginationQuery
{
    public Guid[]? Ids { get; init; }
    public Guid[]? MediaIds { get; init; }
    public HashSet<PersonRoleType>? RoleTypes { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}
