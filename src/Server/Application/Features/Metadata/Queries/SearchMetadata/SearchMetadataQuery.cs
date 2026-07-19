using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Metadata.Queries.SearchMetadata;

public class SearchMetadataQuery : IRequest<IEnumerable<MetadataSearchResult>>
{
    public string? Query { get; init; }
    public int? Year { get; init; }
    public string? ProviderId { get; init; }
    public MediaType? MediaType { get; init; }
    public string? Language { get; init; }
    public Guid? LibraryId { get; init; }
}

public class SearchMetadataQueryHandler(
    IEnumerable<ISearchableMetadataProvider> metadataProviders,
    IApplicationDbContext context)
    : IRequestHandler<SearchMetadataQuery, IEnumerable<MetadataSearchResult>>
{
    public async Task<IEnumerable<MetadataSearchResult>> Handle(SearchMetadataQuery request, CancellationToken cancellationToken)
    {
        Library? library = null;
        if (request.LibraryId.HasValue)
        {
            library = await context.Libraries
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == request.LibraryId.Value, cancellationToken);
        }

        var language = request.Language;
        if (string.IsNullOrWhiteSpace(language))
            language = library?.MetadataLanguage;

        language = string.IsNullOrWhiteSpace(language) ? "en" : language;

        var fallbackLanguage = library?.MetadataFallbackLanguage;
        if (string.IsNullOrWhiteSpace(fallbackLanguage))
            fallbackLanguage = null;

        var mediaType = request.MediaType ?? library?.MediaType switch
        {
            LibraryMediaType.Movie => MediaType.Movie,
            LibraryMediaType.Serie => MediaType.Serie,
            LibraryMediaType.Music => MediaType.MusicAlbum,
            _ => null
        };

        IEnumerable<ISearchableMetadataProvider> applicableProviders = metadataProviders;
        if (library is not null)
        {
            var normalizedProvider = MetadataProviderNames.Normalize(library.MetadataProviderName);
            applicableProviders = metadataProviders
                .Where(p => string.Equals(
                    MetadataProviderNames.Normalize(p.ProviderName),
                    normalizedProvider,
                    StringComparison.OrdinalIgnoreCase));
        }

        var providerList = applicableProviders.ToList();
        if (providerList.Count == 0)
            return [];

        var tasks = providerList.Select(provider => provider.SearchMetadataAsync(
            request.Query ?? string.Empty,
            request.Year,
            request.ProviderId,
            mediaType,
            language,
            fallbackLanguage,
            cancellationToken));
        var results = await Task.WhenAll(tasks);
        var flattened = results.SelectMany(r => r);

        if (string.IsNullOrWhiteSpace(request.Query) || !string.IsNullOrWhiteSpace(request.ProviderId))
            return flattened;

        return MetadataTitleMatchHelper.OrderByBestMatch(
            request.Query,
            request.Year,
            flattened,
            result => result.Title,
            result => result.Year);
    }
}
