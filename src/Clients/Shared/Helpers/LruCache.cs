namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Small thread-unsafe LRU map for UI-side page caches (Blazor circuit is single-threaded).
/// </summary>
public sealed class LruCache<TKey, TValue>
    where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map;
    private readonly LinkedList<(TKey Key, TValue Value)> _order = new();

    public LruCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _map = new Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>>(capacity);
    }

    public int Count => _map.Count;

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_map.TryGetValue(key, out var node))
        {
            _order.Remove(node);
            _order.AddFirst(node);
            value = node.Value.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        if (_map.TryGetValue(key, out var existing))
        {
            existing.Value = (key, value);
            _order.Remove(existing);
            _order.AddFirst(existing);
            return;
        }

        if (_map.Count >= _capacity)
        {
            var last = _order.Last!;
            _order.RemoveLast();
            _map.Remove(last.Value.Key);
        }

        var node = _order.AddFirst((key, value));
        _map[key] = node;
    }

    public void Clear()
    {
        _map.Clear();
        _order.Clear();
    }

    public List<(TKey Key, TValue Value)> Snapshot()
    {
        var result = new List<(TKey Key, TValue Value)>(_order.Count);
        foreach (var entry in _order)
            result.Add(entry);
        return result;
    }
}
