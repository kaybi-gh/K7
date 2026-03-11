namespace K7.Shared;

public static class CamelotWheel
{
    private static readonly Dictionary<string, (int Number, char Letter)> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A flat minor"] = (1, 'A'), ["G sharp minor"] = (1, 'A'),
        ["B major"] = (1, 'B'),
        ["E flat minor"] = (2, 'A'), ["D sharp minor"] = (2, 'A'),
        ["F sharp major"] = (2, 'B'), ["G flat major"] = (2, 'B'),
        ["B flat minor"] = (3, 'A'), ["A sharp minor"] = (3, 'A'),
        ["D flat major"] = (3, 'B'), ["C sharp major"] = (3, 'B'),
        ["F minor"] = (4, 'A'),
        ["A flat major"] = (4, 'B'), ["G sharp major"] = (4, 'B'),
        ["C minor"] = (5, 'A'),
        ["E flat major"] = (5, 'B'), ["D sharp major"] = (5, 'B'),
        ["G minor"] = (6, 'A'),
        ["B flat major"] = (6, 'B'), ["A sharp major"] = (6, 'B'),
        ["D minor"] = (7, 'A'),
        ["F major"] = (7, 'B'),
        ["A minor"] = (8, 'A'),
        ["C major"] = (8, 'B'),
        ["E minor"] = (9, 'A'),
        ["G major"] = (9, 'B'),
        ["B minor"] = (10, 'A'),
        ["D major"] = (10, 'B'),
        ["F sharp minor"] = (11, 'A'), ["G flat minor"] = (11, 'A'),
        ["A major"] = (11, 'B'),
        ["C sharp minor"] = (12, 'A'), ["D flat minor"] = (12, 'A'),
        ["E major"] = (12, 'B'),
    };

    /// <summary>
    /// Two keys are harmonically compatible if they share the same Camelot position,
    /// are ±1 on the wheel (same letter, wrapping 12→1),
    /// or have the same number with opposite letter (relative major/minor).
    /// </summary>
    public static bool AreKeysCompatible(string? keyA, string? keyB)
    {
        if (keyA is null || keyB is null) return false;
        if (!Map.TryGetValue(keyA, out var a) || !Map.TryGetValue(keyB, out var b))
            return false;

        if (a == b) return true;

        if (a.Letter == b.Letter)
        {
            var diff = Math.Abs(a.Number - b.Number);
            if (diff == 1 || diff == 11) return true;
        }

        if (a.Number == b.Number && a.Letter != b.Letter) return true;

        return false;
    }
}
