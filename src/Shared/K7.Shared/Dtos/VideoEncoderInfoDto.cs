namespace K7.Shared.Dtos;

public sealed record VideoEncoderInfoDto
{
    public required string EncoderName { get; init; }
    public required bool IsHardwareAccelerated { get; init; }
}
