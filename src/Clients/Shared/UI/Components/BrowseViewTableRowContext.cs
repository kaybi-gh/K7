namespace K7.Clients.Shared.UI.Components;

public sealed class BrowseViewTableRowContext<TItem>
{
    public required TItem Item { get; init; }
    public int Index { get; init; }

    public bool IsAlternate => Index % 2 == 1;
}
