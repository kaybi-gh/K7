using K7.Server.Domain.Entities;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class ExternalIdMappings
{
    extension(ExternalId domain)
    {
        public ExternalIdDto ToExternalIdDto() => new()
        {
            Id = domain.Id,
            ProviderName = domain.ProviderName,
            Value = domain.Value
        };
    }
}
