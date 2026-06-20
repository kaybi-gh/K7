namespace K7.Shared.Dtos.Requests;

public sealed record UpdateLibraryGroupRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string? CardColor { get; init; }
}
