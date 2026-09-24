namespace K7.Clients.Shared.UI.Helpers;

internal static class BrowseAlphabetJump
{
    public static readonly IReadOnlyList<string> Labels =
        ["#", .. Enumerable.Range('A', 26).Select(c => ((char)c).ToString())];

    public static int FindIndexForLetter(IReadOnlyList<string> titles, string label, bool ascending)
    {
        if (titles.Count == 0)
            return 0;

        if (label == "#")
            return ascending ? 0 : titles.Count - 1;

        var targetChar = char.ToUpperInvariant(label[0]);
        var low = 0;
        var high = titles.Count - 1;
        var result = ascending ? titles.Count - 1 : 0;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            var title = titles[mid];
            var itemChar = title.Length > 0 ? char.ToUpperInvariant(title[0]) : '#';
            var isLetter = char.IsLetter(itemChar);

            int cmp;
            if (!isLetter && targetChar == '#')
                cmp = 0;
            else if (!isLetter)
                cmp = ascending ? -1 : 1;
            else
                cmp = ascending
                    ? itemChar.CompareTo(targetChar)
                    : targetChar.CompareTo(itemChar);

            if (cmp < 0)
                low = mid + 1;
            else
            {
                result = mid;
                high = mid - 1;
            }
        }

        return Math.Clamp(result, 0, titles.Count - 1);
    }

    public static string GetSortTitle(string? sortTitle, string? title) =>
        sortTitle ?? title ?? "";
}
