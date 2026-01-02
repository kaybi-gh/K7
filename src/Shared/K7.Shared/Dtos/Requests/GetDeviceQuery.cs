namespace K7.Shared.Dtos.Requests;

public sealed record GetDeviceQuery
{
    public Guid Id { get; init; }
}
