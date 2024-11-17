using K7.Clients.Shared.Domain.Models;

namespace K7.Clients.Shared.Domain.Interfaces;

public interface IMediaServerService
{
    Task<Movie?> GetMovieAsync(Guid id);
    Task<PaginatedList<LiteMedia>?> GetLiteMediasAsync(GetLiteMediasQuery query);
    Task<Person?> GetPersonAsync(Guid id);
}
