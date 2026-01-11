using System.Net.Http.Json;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Entities.Medias;
using System.Text.Json;
using System.Text.Json.Serialization;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.QueryBuilders;

namespace K7.Shared.Services;

public class K7ServerService : IK7ServerService
{
    public HttpClient HttpClient { get; }
    private readonly JsonSerializerOptions _serializerOptions;

    public K7ServerService(HttpClient httpClient)
    {
        HttpClient = httpClient;

        _serializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            PropertyNameCaseInsensitive = true
        };
        _serializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public Uri? GetAbsoluteUri(string? relativePath = null)
    {
        return HttpClient.BaseAddress != null && !string.IsNullOrEmpty(relativePath)
            ? new Uri(HttpClient.BaseAddress, relativePath)
            : HttpClient.BaseAddress;
    }

    public async Task<Guid> CreateDeviceAsync(CreateDeviceRequest request)
    {
        var requestUri = CreateDeviceRequestUriBuilder.Route;
        var responseMessage = await HttpClient.PostAsJsonAsync(requestUri, request, _serializerOptions);
        responseMessage.EnsureSuccessStatusCode();
        var result = await responseMessage.Content.ReadFromJsonAsync<GetDeviceQuery>(_serializerOptions);
        return result!.Id;
    }

    public async Task AttachCurrentUserToDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var responseMessage = await HttpClient.PostAsync($"/api/devices/{deviceId}/attach-user", null, cancellationToken);
        responseMessage.EnsureSuccessStatusCode();
    }

    public async Task<List<MediaFormatDto>> GetMediaFormatsAsync()
    {
        var formats = await HttpClient.GetFromJsonAsync<List<MediaFormatDto>>("api/media-formats", _serializerOptions);
        return formats ?? [];
    }

    public async Task<MovieDto?> GetMovieAsync(Guid id)
    {
        try
        {
            return await HttpClient.GetFromJsonAsync<MovieDto>($"api/medias/{id}", _serializerOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }

    public async Task<PaginatedListDto<LiteMediaDto>?> GetLiteMediasAsync(GetMediasWithPaginationQuery query)
    {
        var requestUri = GetMediasWithPaginationQueryUriBuilder.Build(query);
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<LiteMediaDto>>(requestUri, _serializerOptions);
    }

    public async Task<PersonDto?> GetPersonAsync(Guid id)
    {
        return await HttpClient.GetFromJsonAsync<PersonDto>($"api/persons/{id}", _serializerOptions);
    }
}
