namespace K7.Shared.Dtos;

public sealed record ActiveStreamDto
{
    public required string ConnectionId { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public Guid? MediaId { get; init; }
    public string? MediaTitle { get; init; }
    public string? MediaType { get; init; }
    public Guid? ParentId { get; init; }
    public Guid? DeviceId { get; init; }
    public string? DeviceName { get; init; }
    public string? DeviceType { get; init; }
    public string? ThumbnailUrl { get; init; }
    public StreamDecisionDto? StreamDecision { get; init; }
    public DateTime StartedAt { get; init; }
    public double Position { get; init; }
    public double Duration { get; init; }
    public int State { get; init; }
}
