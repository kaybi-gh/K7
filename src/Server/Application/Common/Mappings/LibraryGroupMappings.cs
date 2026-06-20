using K7.Server.Domain.Entities;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class LibraryGroupMappings
{
    extension(LibraryGroup domain)
    {
        public LibraryGroupDto ToLibraryGroupDto() => new()
        {
            Id = domain.Id,
            Title = domain.Title,
            MediaType = domain.MediaType,
            Description = domain.Description,
            Icon = domain.Icon,
            CardColor = domain.CardColor,
            CoverPictureId = domain.CoverPicture?.Id,
            CoverDominantColor = domain.CoverPicture?.DominantColor,
            LibraryIds = domain.Libraries.Select(l => l.Id).ToList()
        };
    }
}
