namespace K7.Server.Application.Common.Models;

public static class PagingDefaults
{
    public const int DefaultPageSize = 20;
    public const int CompactPageSize = 10;
    public const int HistoryPageSize = 25;
    public const int ItemsPageSize = 50;
    public const int MaxPageSize = 100;

    public static int ClampPageNumber(int pageNumber) => Math.Max(1, pageNumber);

    public static int ClampPageSize(int pageSize) => Math.Clamp(pageSize, 1, MaxPageSize);
}
