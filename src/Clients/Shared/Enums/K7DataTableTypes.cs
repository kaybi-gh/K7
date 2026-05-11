namespace K7.Clients.Shared.Enums;

public enum K7SortDirection
{
    Ascending,
    Descending
}

public sealed record SortChangedEventArgs(string SortKey, K7SortDirection Direction);

public sealed record K7DataTableState<TItem>(int StartIndex, int Count, string? SortKey, K7SortDirection SortDirection);

public sealed record K7DataTableResult<TItem>(IReadOnlyCollection<TItem> Items, int TotalItemCount);
