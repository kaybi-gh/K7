namespace K7.Clients.Shared.Services.MediaServer.Dtos;

public record ExternalIdDto
{
    public required string Platform { get; init; }
    public required string Value { get; init; }
}
