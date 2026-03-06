namespace K7.Shared.Dtos.Requests;

public sealed record ReidentifyMediaRequest
{
    public required string SelectedProvider { get; init; }
    public required string SelectedExternalId { get; init; }
}
