using AutoMapper;

namespace K7.Clients.Shared.Services.MediaServer.Mappings;

public class PaginatedListConverter<TSource, TDestination> : ITypeConverter<Dtos.PaginatedList<TSource>, Domain.Models.PaginatedList<TDestination>>
{
    public Domain.Models.PaginatedList<TDestination> Convert(Dtos.PaginatedList<TSource> source, Domain.Models.PaginatedList<TDestination> destination, ResolutionContext context)
    {
        return new Domain.Models.PaginatedList<TDestination>()
        {
            Items = context.Mapper.Map<IReadOnlyCollection<TSource>, IReadOnlyCollection<TDestination>>(source.Items),
            PageNumber = source.PageNumber,
            TotalPages = source.TotalPages,
            TotalCount = source.TotalCount,
            HasPreviousPage = source.HasPreviousPage,
            HasNextPage = source.HasNextPage
        };
    }
}
