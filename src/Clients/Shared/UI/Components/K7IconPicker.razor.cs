using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7IconPicker
{
    private const int DefaultDisplayCount = 60;

    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public string SearchPlaceholder { get; set; } = "Search an icon...";

    private string _search = "";

    private static readonly IReadOnlyList<IconEntry> _allIcons = LoadAllIcons();

    private IEnumerable<IconEntry> FilteredIcons
    {
        get
        {
            var q = _search.Trim();
            if (string.IsNullOrEmpty(q))
                return _allIcons.Take(DefaultDisplayCount);
            return _allIcons.Where(i => i.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? "";
    }

    private async Task Select(string value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
    }

    private async Task Clear()
    {
        Value = null;
        _search = "";
        await ValueChanged.InvokeAsync(null);
    }

    private static IReadOnlyList<IconEntry> LoadAllIcons()
    {
        return typeof(Phosphor)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f =>
            {
                var cssValue = (string)f.GetRawConstantValue()!;
                var displayName = cssValue.Replace("ph ph-", "");
                return new IconEntry(displayName, cssValue);
            })
            .OrderBy(i => i.DisplayName)
            .ToList();
    }

    private sealed record IconEntry(string DisplayName, string Value);
}
