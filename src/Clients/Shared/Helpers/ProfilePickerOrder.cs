namespace K7.Clients.Shared.Helpers;

public static class ProfilePickerOrder
{
    public static string User(string identityUserId) => "user:" + identityUserId;

    public static string Group(Guid id) => "group:" + id.ToString("D");

    public static List<T> Arrange<T>(IReadOnlyList<T> items, Func<T, string> token, IReadOnlyList<string> savedOrder)
    {
        var rank = new Dictionary<string, int>(savedOrder.Count, StringComparer.Ordinal);
        for (var i = 0; i < savedOrder.Count; i++)
            rank.TryAdd(savedOrder[i], i);

        return items
            .Select((item, index) => (item, index))
            .OrderBy(row => rank.TryGetValue(token(row.item), out var place) ? place : int.MaxValue)
            .ThenBy(row => row.index)
            .Select(row => row.item)
            .ToList();
    }
}
