namespace K7.Shared.Dtos.Responses;

public sealed record ExternalIdMatchResult
{
    public required string Provider { get; init; }
    public required string Value { get; init; }
    public Guid? MediaId { get; init; }
    /// <summary>
    /// True when the matched media has local indexed files. Prefer playable matches over
    /// virtual (file-less) medias created by a previous import with the same external id.
    /// </summary>
    public bool HasIndexedFiles { get; init; }
}
