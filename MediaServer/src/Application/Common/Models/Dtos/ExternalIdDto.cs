using MediaServer.Domain.Entities;

namespace MediaServer.Application.Common.Models.Dtos;

public record ExternalIdDto
{
    public required string Platform { get; init; }
    public required string Value { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<ExternalId, ExternalIdDto>();
        }
    }
}
