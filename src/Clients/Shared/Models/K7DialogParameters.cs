using System.Collections;
using System.Linq.Expressions;

namespace K7.Clients.Shared.Models;

public class K7DialogParameters : IEnumerable
{
    private readonly Dictionary<string, object?> _dict = new();

    public object? this[string key]
    {
        get => _dict.TryGetValue(key, out var v) ? v : null;
        set => _dict[key] = value;
    }

    public void Add(string key, object? value) => _dict[key] = value;

    public K7DialogParameters Set(string key, object? value)
    {
        _dict[key] = value;
        return this;
    }

    public IDictionary<string, object?> ToDictionary() => _dict;

    IEnumerator IEnumerable.GetEnumerator() => _dict.GetEnumerator();
}

public sealed class K7DialogParameters<TDialog> : K7DialogParameters
{
    public void Add<TValue>(Expression<Func<TDialog, TValue>> property, TValue value)
    {
        var memberExpr = (MemberExpression)property.Body;
        Set(memberExpr.Member.Name, value);
    }
}
