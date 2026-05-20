using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record ImportMediaPictureFromUrlRequest
{
    public required string Url { get; init; }
    public required MetadataPictureType PictureType { get; init; }
}
