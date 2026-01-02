using K7.Server.Application.Common.Models;
using K7.Shared.Dtos;

namespace K7.Server.Web.Converters;

public static class PaginatedListExtensions
{
    public static PaginatedListDto<TDto> ToDto<T, TDto>(
        this PaginatedList<T> paginatedList,
        Func<T, TDto> mapper)
    {
        return new PaginatedListDto<TDto>()
        {
            Items = paginatedList.Items.Select(mapper).ToList().AsReadOnly(),
            PageNumber = paginatedList.PageNumber,
            TotalCount = paginatedList.TotalCount,
            TotalPages = paginatedList.TotalPages
        };
    }
}

