using K7.Server.Domain.Entities.Federation;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class FederationMappings
{
    extension(PeerServer domain)
    {
        public PeerServerDto ToPeerServerDto() => new()
        {
            Id = domain.Id,
            Name = domain.Name,
            BaseUrl = domain.BaseUrl,
            Status = domain.Status,
            LastSeen = domain.LastSeen,
            Created = domain.Created,
            ShareAgreements = domain.ShareAgreements
                .Select(a => a.ToPeerShareAgreementDto())
                .ToList()
        };
    }

    extension(PeerShareAgreement domain)
    {
        public PeerShareAgreementDto ToPeerShareAgreementDto() => new()
        {
            Id = domain.Id,
            LibraryId = domain.LibraryId,
            LibraryTitle = domain.Library?.Title,
            Direction = domain.Direction,
            MaxConcurrentStreams = domain.MaxConcurrentStreams,
            IsEnabled = domain.IsEnabled
        };
    }

    extension(PeerRequest domain)
    {
        public PeerRequestDto ToPeerRequestDto() => new()
        {
            Id = domain.Id,
            RequesterUrl = domain.RequesterUrl,
            RequesterName = domain.RequesterName,
            Status = domain.Status,
            Created = domain.Created,
            RespondedAt = domain.RespondedAt
        };
    }
}
