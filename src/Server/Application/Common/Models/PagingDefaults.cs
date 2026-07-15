namespace K7.Server.Application.Common.Models;

public static class PagingDefaults
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static int ClampPageNumber(int pageNumber) => Math.Max(1, pageNumber);

    public static int ClampPageSize(int pageSize) => Math.Clamp(pageSize, 1, MaxPageSize);
}
