namespace K7.Shared.Dtos;

public sealed record AudioMuseAiConnectionResultDto
{
    public bool Success { get; init; }
    public string? Version { get; init; }
    public string? Error { get; init; }
}
