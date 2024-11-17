using AutoMapper;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;
using K7.Clients.Shared.Services.MediaServer.Dtos;
using System.Net.Http.Json;

namespace K7.Clients.Shared.Services.MediaServer;

public class MediaServerService(HttpClient client, IMapper mapper) : IMediaServerService
{
    private readonly HttpClient _httpClient = client;
    private readonly IMapper _mapper = mapper;

    public string GetBaseUrl()
        => _httpClient.BaseAddress!.OriginalString;

    public async Task<Movie?> GetMovieAsync(Guid id)
    {
        try
        {
            var media = await _httpClient.GetFromJsonAsync<MediaDto>($"api/medias/{id}");
            return _mapper.Map<Movie?>(media);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }

    public async Task<Domain.Models.PaginatedList<LiteMedia>?> GetLiteMediasAsync(GetLiteMediasQuery query)
    {
        var queryDto = _mapper.Map<GetMediasWithPaginationQuery>(query);
        var page = await _httpClient.GetFromJsonAsync<Dtos.PaginatedList<LiteMediaDto>>($"api/medias?PageNumber={queryDto.PageNumber}&PageSize={queryDto.PageSize}");
        return _mapper.Map<Dtos.PaginatedList<LiteMediaDto>?, Domain.Models.PaginatedList<LiteMedia>?>(page);
    }

    public async Task<Person?> GetPersonAsync(Guid id)
    {
        var personDto = await _httpClient.GetFromJsonAsync<PersonDto>($"api/persons/{id}");
        return _mapper.Map<Person?>(personDto);
    }
}
