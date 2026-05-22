namespace K7.Server.Domain.Entities.Metadatas.External;

public record ExternalPersonDetails
{
    public string? Biography { get; init; }
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }
    public PersonGender Gender { get; init; }
    public string? ImageUrl { get; init; }
    public IReadOnlyList<ExternalIdEntry> AdditionalExternalIds { get; init; } = [];
}

public record ExternalIdEntry(string ProviderName, string Value);
