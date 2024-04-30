namespace MediaClient.Shared.Domain.Models;

public class PersonRole
{
    public required string Id { get; set; }
    public Guid PersonId { get; init; }
    public string PersonSlug { get; init; } = null!;
    public string PersonName { get; init; } = null!;
    public string? CharacterName { get; init; }
    public string PortraitPicture { get; set; } = "";
}
