using K7.Server.Domain.Entities;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class LibraryMappings
{
    extension(Library domain)
    {
        public LibraryDto ToLibraryDto() => new()
        {
            Id = domain.Id,
            Title = domain.Title,
            MediaType = domain.MediaType,
            RootPath = domain.RootPath,
            MetadataProviderName = domain.MetadataProviderName,
            MetadataLanguage = domain.MetadataLanguage,
            MetadataFallbackLanguage = domain.MetadataFallbackLanguage,
            MetadataRefreshIntervalDays = domain.MetadataRefreshIntervalDays,
            LibraryGroupId = domain.LibraryGroupId
        };
    }
}
