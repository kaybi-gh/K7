using AutoMapper;
using MediaClient.Shared.Services.MediaServer.Dtos;
using Microsoft.Extensions.Options;

namespace MediaClient.Shared.Services.MediaServer.Mappings;

public class MediaServerBaseUrlPathResolver(IOptions<MediaServerConfiguration> mediaServerConfiguration) : IMemberValueResolver<object, object, MetadataPictureDto, string?>
{
    private readonly MediaServerConfiguration _mediaServerConfiguration = mediaServerConfiguration.Value;

    public string? Resolve(object source, object destination, MetadataPictureDto sourceMember, string? destMember, ResolutionContext context)
    {
        var baseUrl = _mediaServerConfiguration.BaseUrl;
        var backgroundUri = sourceMember.Uri?.OriginalString;
        return $"{baseUrl}{backgroundUri}";
    }
}
