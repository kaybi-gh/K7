namespace K7.Clients.Shared.Models;

public sealed record ThemeDefinition(string Name, string CssDataAttribute)
{
    public string? CustomCss { get; init; }
}
