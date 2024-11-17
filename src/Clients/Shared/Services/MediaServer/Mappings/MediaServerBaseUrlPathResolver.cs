using AutoMapper;
using K7.Clients.Shared.Services.MediaServer.Dtos;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Linq.Expressions;

namespace K7.Clients.Shared.Services.MediaServer.Mappings;

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

public class MediaServerAbsoluteUriResolver(IOptions<MediaServerConfiguration> config) : IMemberValueResolver<object, object, Uri?, string?>
{
    private readonly MediaServerConfiguration _config = config.Value;

    public string? Resolve(object source, object destination, Uri? sourceMember, string? destMember, ResolutionContext context)
    {
        if (sourceMember == null)
        {
            return null;
        }

        if (sourceMember.IsAbsoluteUri)
        {
            return sourceMember.AbsoluteUri;
        }

        return new Uri(new Uri(_config.BaseUrl), sourceMember).AbsoluteUri;
    }
}

public class MediaServerAbsoluteUriListResolver(IOptions<MediaServerConfiguration> config) : IMemberValueResolver<object, object, IEnumerable<Uri?>?, IEnumerable<string>>
{
    private readonly MediaServerConfiguration _config = config.Value;

    public IEnumerable<string> Resolve(object source, object destination, IEnumerable<Uri?>? sourceMember, IEnumerable<string> destMember, ResolutionContext context)
    {
        if (sourceMember == null)
        {
            return [];
        }
        var baseUrl = new Uri(_config.BaseUrl);

        return sourceMember
            .Where(x => x != null)
            .Select(x => new Uri(baseUrl, x!).AbsoluteUri);
        // TODO - Make it better
    }
}