using MediaClient.Shared.Domain.Models;

namespace MediaClient.Shared.Domain.Interfaces;

public interface IMediaServerService
{
    Task<Movie?> GetMovieAsync(Guid id);
    Task<PaginatedList<LiteMedia>?> GetLiteMediasAsync(GetLiteMediasQuery query);
    Task<Person?> GetPersonAsync(Guid id);
}
