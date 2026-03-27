using K7.Clients.Shared.Enums;

namespace K7.Clients.Shared.Models;

public abstract record LitePersonRole
{
    public string Id { get; init; } = null!;
    public string MediaId { get; init; } = null!;
    public PersonRoleType Type { get; init; }
    public int? Order { get; init; }
    public string? PortraitPictureHref { get; init; }
    public LitePerson? Person { get; init; }
}
