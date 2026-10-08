namespace K7.Shared.Dtos.Federation;

public sealed record HlsSegmentDto
{
    public required int Number { get; init; }
    public required long StartTimestamp { get; init; }
    public required long Duration { get; init; }
}
